package com.ciai.controller.sdk.webserver;

import com.ciai.controller.sdk.core.DeviceDriverBase;
import com.ciai.controller.sdk.model.Result;
import com.ciai.controller.sdk.model.RegisterInfo;
import com.ciai.controller.sdk.model.HeartBeatInfo;
import com.ciai.controller.sdk.model.GetReturn;
import com.ciai.controller.sdk.logging.LoggerProvider;
import org.slf4j.Logger;

import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.List;

/**
 * 路由构建器
 */
public class RouteBuilder {

    private static final Logger logger = LoggerProvider.createLogger(RouteBuilder.class);

    /**
     * API端点常量
     */
    public static class Endpoints {
        public static final String INFO = "/Info";
        public static final String HEART_BEAT = "/HeartBeat";
        public static final String FUNCTION = "/Function";
        public static final String OPERATION = "/Operation";
        public static final String SET = "/Set";
        public static final String GET = "/Get";
        public static final String ENTER_AND_EXIT = "/EnterAndExit";
    }

    /**
     * 路由信息
     */
    public static class RouteInfo {
        private final String path;
        private final String method;
        private final RouteHandler handler;

        public RouteInfo(String path, String method, RouteHandler handler) {
            this.path = path;
            this.method = method;
            this.handler = handler;
        }

        public String getPath() {
            return path;
        }

        public String getMethod() {
            return method;
        }

        public RouteHandler getHandler() {
            return handler;
        }
    }

    /**
     * 路由处理器接口
     */
    @FunctionalInterface
    public interface RouteHandler {
        HttpResponse handle(String body) throws Exception;
    }

    /**
     * 构建路由
     */
    public static List<RouteInfo> buildRoutes(DeviceDriverBase driver) {
        List<RouteInfo> routes = new ArrayList<>();

        // Info端点
        routes.add(new RouteInfo(Endpoints.INFO, "GET", body -> {
            try {
                Result<RegisterInfo> result = driver.getRegisterInfo();
                return HttpResponse.ok(result);
            } catch (Exception e) {
                logger.error("Handle Info error", e);
                return HttpResponse.internalError(e.getMessage());
            }
        }));

        // HeartBeat端点
        routes.add(new RouteInfo(Endpoints.HEART_BEAT, "GET", body -> {
            try {
                Result<HeartBeatInfo> result = driver.getHeartBeat();
                return HttpResponse.ok(result);
            } catch (Exception e) {
                logger.error("Handle HeartBeat error", e);
                return HttpResponse.internalError(e.getMessage());
            }
        }));

        // Function端点
        routes.add(new RouteInfo(Endpoints.FUNCTION, "POST", body -> {
            try {
                return RequestHandler.handleFunction(driver, body);
            } catch (Exception e) {
                logger.error("Handle Function error", e);
                return HttpResponse.internalError(e.getMessage());
            }
        }));

        // Operation端点
        routes.add(new RouteInfo(Endpoints.OPERATION, "POST", body -> {
            try {
                return RequestHandler.handleOperation(driver, body);
            } catch (Exception e) {
                logger.error("Handle Operation error", e);
                return HttpResponse.internalError(e.getMessage());
            }
        }));

        // Set端点
        routes.add(new RouteInfo(Endpoints.SET, "POST", body -> {
            try {
                return RequestHandler.handleSet(driver, body);
            } catch (Exception e) {
                logger.error("Handle Set error", e);
                return HttpResponse.internalError(e.getMessage());
            }
        }));

        // Get端点
        routes.add(new RouteInfo(Endpoints.GET, "GET", body -> {
            try {
                Result<List<GetReturn>> result = driver.getStatus();
                return HttpResponse.ok(result);
            } catch (Exception e) {
                logger.error("Handle Get error", e);
                return HttpResponse.internalError(e.getMessage());
            }
        }));

        // EnterAndExit端点
        routes.add(new RouteInfo(Endpoints.ENTER_AND_EXIT, "POST", body -> {
            try {
                return RequestHandler.handleEnterAndExit(driver, body);
            } catch (Exception e) {
                logger.error("Handle EnterAndExit error", e);
                return HttpResponse.internalError(e.getMessage());
            }
        }));

        return routes;
    }
}
