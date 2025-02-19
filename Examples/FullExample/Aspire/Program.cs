using Projects;

var builder = DistributedApplication.CreateBuilder(args);


var rabbitUser = builder.AddParameter("RabbitUser", () => "TestUser");
var rabbitPass = builder.AddParameter("RabbitPassword", () => "MySuperPassword", true);


var rabbitmq = builder.AddRabbitMQ("messaging", rabbitUser, rabbitPass, 5672)
    .WithManagementPlugin(15672)
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Service1>("Service1")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("BrokerConnection:UserName", rabbitUser)
    .WithEnvironment("BrokerConnection:Password", rabbitPass)
    .WithEnvironment("BrokerConnection:BrokerEndPoints:0", "amqp://localhost:5672")
    .WithEnvironment("BrokerConnection:VHost", "/");



builder.AddProject<Service2>("Service2")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("BrokerConnection:UserName", rabbitUser)
    .WithEnvironment("BrokerConnection:Password", rabbitPass)
    .WithEnvironment("BrokerConnection:BrokerEndPoints:0", "amqp://localhost:5672")
    .WithEnvironment("BrokerConnection:VHost", "/");



builder.AddProject<Client>("Client1")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("BrokerConnection:UserName", rabbitUser)
    .WithEnvironment("BrokerConnection:Password", rabbitPass)
    .WithEnvironment("BrokerConnection:BrokerEndPoints:0", "amqp://localhost:5672")
    .WithEnvironment("BrokerConnection:VHost", "/");



builder.Build().Run();