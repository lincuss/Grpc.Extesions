[![Nuget](https://img.shields.io/nuget/v/Grpc.svg)](https://www.nuget.org/packages/FollowmeTech.Grpc.Extension/)gRPC extensions

一个基于GRPC的简单微服务框架 

## 功能
- 服务注册和发现
- 服务自动负载均衡
- 服务端中件间(性能监控[日志],全局错误处理,手动熔断)
- 客户端中件间(认证，超时时间设置)
- DashBoard(远程调用，手动熔断，日志输出控制)

## 依赖的技术栈
-  [dotnet standard 2.0]()
-  [gRPC - An RPC library and framework](https://github.com/grpc/grpc)
-  [consul-Service Discovery and Configuration Made Easy](https://consul.io)


## todo
- [ ] code first (生成proto file)
- [ ] proto first (使用protoc 根据proto文件生成cs文件)
- [ ] publish package on nuget.com
- [ ] 取消logAccessor ,采用log middleware


## 感谢
感谢以下的项目,排名不分先后
