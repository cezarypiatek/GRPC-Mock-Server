# GrpcToRestProxyGenerator

## How to run Grpc-To-Rest-Proxy

```
docker run -it -p 5033:5033 -p 9095:9095 -v c:\tmp\prototest:/protos cezarypiatek/grpc-mock-server
```

Ports:
- 5033 for GRPC
- 9095 for Stubbing (WireMock API)