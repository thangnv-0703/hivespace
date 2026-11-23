using FakeIdp;

var app = FakeIdpApp.Build(args);
await app.RunAsync();
