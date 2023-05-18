# GRPC-Mock-Server
Super fast, platform independent, standalone component for mocking GRPC services using [WireMock.NET](https://github.com/WireMock-Net/WireMock.Net) stubbing engine


## How to run GRPC-Mock-Server

```
docker run -it -p 5033:5033 -p 9095:9095 -v $(pwd)/protos:/protos cezarypiatek/grpc-mock-server
```

Ports:
- 5033 for GRPC
- 9095 for Stubbing (WireMock API)

## How to prepare mocks

Use the WireMock Rest API to prepare mappings.

## How does it work

GRPC-Mock-Server works in the following way:
- compile provided `*.proto` files
- generate proxy for every service and method defined in the `*.proto` files
- use the generated proxy to translate GRPC calls to REST and forward it to `WireMock` backend

## Supported GRPC communication patterns

|Pattern|Implementation status|
|---|----|
|request-reply|✅|
|server-streaming|✅|
|client-streaming|✅|
|client-server-streaming|❌|


## TODO
- [ ] Implement error response codes
- [ ] Implement library that wraps WireMock API for stubbing
- [ ] Implement test container

## Alternatives
- https://github.com/Adven27/grpc-wiremock
- https://github.com/tokopedia/gripmock
