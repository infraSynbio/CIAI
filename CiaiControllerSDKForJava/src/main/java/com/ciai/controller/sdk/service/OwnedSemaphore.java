package com.ciai.controller.sdk.service;

import java.util.concurrent.Semaphore;
import java.util.concurrent.TimeUnit;

/**
 * Compatibility semaphore that only lets the acquiring thread release a permit.
 * New drivers should prefer SDK connection/device-call resource helpers.
 */
public final class OwnedSemaphore extends Semaphore {
    private static final long serialVersionUID = 1L;
    private final ThreadLocal<Integer> ownedPermits = new ThreadLocal<Integer>();

    public OwnedSemaphore(int permits, boolean fair) {
        super(permits, fair);
        if (permits <= 0) throw new IllegalArgumentException("permits must be greater than zero");
    }

    @Override
    public void acquire() throws InterruptedException {
        super.acquire();
        recordAcquire(1);
    }

    @Override
    public void acquireUninterruptibly() {
        super.acquireUninterruptibly();
        recordAcquire(1);
    }

    @Override
    public void acquire(int permits) throws InterruptedException {
        super.acquire(permits);
        recordAcquire(permits);
    }

    @Override
    public void acquireUninterruptibly(int permits) {
        super.acquireUninterruptibly(permits);
        recordAcquire(permits);
    }

    @Override
    public boolean tryAcquire() {
        boolean acquired = super.tryAcquire();
        if (acquired) recordAcquire(1);
        return acquired;
    }

    @Override
    public boolean tryAcquire(long timeout, TimeUnit unit) throws InterruptedException {
        boolean acquired = super.tryAcquire(timeout, unit);
        if (acquired) recordAcquire(1);
        return acquired;
    }

    @Override
    public boolean tryAcquire(int permits) {
        boolean acquired = super.tryAcquire(permits);
        if (acquired) recordAcquire(permits);
        return acquired;
    }

    @Override
    public boolean tryAcquire(int permits, long timeout, TimeUnit unit) throws InterruptedException {
        boolean acquired = super.tryAcquire(permits, timeout, unit);
        if (acquired) recordAcquire(permits);
        return acquired;
    }

    @Override
    public void release() {
        if (recordRelease(1)) super.release();
    }

    @Override
    public void release(int permits) {
        if (recordRelease(permits)) super.release(permits);
    }

    private void recordAcquire(int count) {
        Integer current = ownedPermits.get();
        ownedPermits.set((current == null ? 0 : current) + count);
    }

    private boolean recordRelease(int count) {
        Integer current = ownedPermits.get();
        if (current == null || current < count) return false;
        int remaining = current - count;
        if (remaining == 0) ownedPermits.remove();
        else ownedPermits.set(remaining);
        return true;
    }
}
