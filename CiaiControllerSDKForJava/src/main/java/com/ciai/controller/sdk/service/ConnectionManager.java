package com.ciai.controller.sdk.service;

import com.ciai.controller.sdk.core.ConnectionConfiguration;
import com.ciai.controller.sdk.interface_.ICommunication;
import java.util.*;
import java.util.concurrent.Semaphore;
import java.util.concurrent.TimeUnit;
import java.util.function.Function;

/** 管理命名连接、共享资源组、重试和生命周期。 */
public class ConnectionManager implements AutoCloseable {
    private static class Entry { ConnectionConfiguration config; ICommunication communication; Semaphore gate; }
    private final Map<String,Entry> entries=new LinkedHashMap<>();
    private final Map<String,Semaphore> groups=new LinkedHashMap<>();
    private String defaultName;
    public ConnectionManager(Collection<ConnectionConfiguration> configurations){
        if(configurations==null||configurations.isEmpty())throw new IllegalArgumentException("At least one connection is required");
        for(ConnectionConfiguration c:configurations){
            String name=c.getName();if(name==null||name.trim().isEmpty())throw new IllegalArgumentException("Connection name is required");
            String key=name.toLowerCase(Locale.ROOT);if(entries.containsKey(key))throw new IllegalArgumentException("Duplicate connection: "+name);
            String group=c.getResourceGroup()==null||c.getResourceGroup().trim().isEmpty()?"@"+key:c.getResourceGroup().toLowerCase(Locale.ROOT);
            Semaphore gate=groups.get(group);if(gate==null){gate=new Semaphore(c.getEffectiveMaxConcurrency(),true);groups.put(group,gate);}
            Entry e=new Entry();e.config=c;e.communication=CommunicationProviderRegistry.create(c);e.gate=gate;entries.put(key,e);
            if(defaultName==null||c.isDefault())defaultName=key;
        }
    }
    public ICommunication get(){return get(null);} public ICommunication get(String name){String key=name==null?defaultName:name.toLowerCase(Locale.ROOT);Entry e=entries.get(key);if(e==null)throw new IllegalArgumentException("Unknown connection: "+name);return e.communication;}
    public boolean isConnected(){for(Entry e:entries.values())if(e.config.isRequired()&&e.config.isConnectOnStart()&&!e.communication.isConnected())return false;return true;}
    public boolean connect(){List<Entry> connected=new ArrayList<>();for(Entry e:entries.values()){if(!e.config.isConnectOnStart())continue;if(e.communication.connect()){connected.add(e);continue;}if(!e.config.isRequired())continue;Collections.reverse(connected);for(Entry x:connected)x.communication.disconnect();return false;}return true;}
    public <T>T execute(String name,Function<ICommunication,T> action){Entry e=entries.get((name==null?defaultName:name).toLowerCase(Locale.ROOT));if(e==null)throw new IllegalArgumentException("Unknown connection: "+name);boolean acquired=false;try{acquired=e.gate.tryAcquire(e.config.getResourceWaitTimeoutMs(),TimeUnit.MILLISECONDS);if(!acquired)throw new IllegalStateException("Connection resource timeout: "+name);if(!e.communication.isConnected()&&!e.communication.connect())throw new IllegalStateException("Connection failed: "+name);RuntimeException last=null;long delay=e.config.getRetryDelayMs();for(int i=0;i<=e.config.getRetryCount();i++){try{return action.apply(e.communication);}catch(RuntimeException ex){last=ex;if(i<e.config.getRetryCount()){try{e.communication.disconnect();e.communication.connect();}catch(RuntimeException ignored){}if(delay>0){Thread.sleep(delay);delay=(long)Math.min(Integer.MAX_VALUE,delay*e.config.getRetryBackoff());}}}}throw last;}catch(InterruptedException ex){Thread.currentThread().interrupt();throw new IllegalStateException("Connection call interrupted",ex);}finally{if(acquired)e.gate.release();}}
    public void disconnect(){List<Entry> list=new ArrayList<>(entries.values());Collections.reverse(list);for(Entry e:list)e.communication.disconnect();}
    public void close(){disconnect();for(Entry e:entries.values())if(e.communication instanceof AutoCloseable)try{((AutoCloseable)e.communication).close();}catch(Exception ignored){}}
}
