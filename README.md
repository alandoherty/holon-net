<div align="center">

[![GitHub license](https://img.shields.io/badge/license-Apache%202-blue.svg?style=flat-square)](https://raw.githubusercontent.com/alandoherty/holon-net/master/LICENSE)
[![GitHub issues](https://img.shields.io/github/issues/alandoherty/holon-net.svg?style=flat-square)](https://github.com/alandoherty/holon-net/issues)
[![GitHub stars](https://img.shields.io/github/stars/alandoherty/holon-net.svg?style=flat-square)](https://github.com/alandoherty/holon-net/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/alandoherty/holon-net.svg?style=flat-square)](https://github.com/alandoherty/holon-net/network)
[![GitHub forks](https://img.shields.io/nuget/dt/Holon.svg?style=flat-square)](https://www.nuget.org/packages/Holon/)

</div>


# Holon

This repository provides an in-development messaging and service layer ontop of RabbitMQ.

## Goals

Holon was created to satisfy a mix-match of goals that are not completely fullfiled by competing libraries. Some are too heavy, others have dependency bloat or inconsistent API's. This library attempts to provide the following tenants:

- Decoupled transport architecture and few dependencies
- No use of language specific technologies
- Support various types of services
- Decoupling remote-procedure call from the service layer
- Event system built-in

## Getting Started

[![NuGet Status](https://img.shields.io/nuget/v/Holon.svg?style=flat-square)](https://www.nuget.org/packages/Holon/)

You can install the package using either the CLI:

```
dotnet add package Holon
```

or from the NuGet package manager:

```
Install-Package Holon
```

## Example

The repository comes with an example project, but a practical example of how this library can be used is documented below. 

## Factory Service

The factory service provides a way to look at the total production and command a factory to start and stop. The implementation here is a console application but it can be embedded anywhere that is async friendly.

```csharp
[RpcContract]
interface IFactoryController 
{
	[RpcOperation]
	Task<double> GetProductionToday();

	[RpcOperation]
	Task StartProduction();

	[RpcOperation]
	Task StopProduction();
}

class FruitFactory : IFactoryController 
{
	public async Task GetProductionToday() {
		return 10000.0;
	}

	public async Task StartProduction() {
		// start our factory up!
	}

	public async Task StopProduction() {
		// shut it down before any fruit gets bruised
	}
}

class ServiceHost {
	static void Main() => MainAsync().Wait();

	static async Task MainAsync() {
		// attach node
		Node node = await Node.CreateAsync("amqp://localhost");

		// attach our service
		await node.AttachAsync("factory:fruit", ServiceType.Singleton, RpcBehaviour.Bind<IFactoryController>(new FruitFactory()));

		// wait forever
		await Task.Delay(Timeout.InfiniteSpan);
	}
}
```

## Client

The client can also be embedded anywhere that is async friendly, and provides a simple way to obtain a proxy and begin communicating with the fruit factory. The interface class is carried over from the previous example.

```csharp
class Client {
	static void Main() => MainAsync().Wait();

	static async Task MainAsync() {
		// attach node
		Node node = await Node.CreateAsync("amqp://localhost");

		// get a proxy to the factory
		IFactoryController controller = node.Proxy<IFactoryController>("factory:fruit");

		// start production!
		await controller.StartProduction();
	}
}
```

## Contributing

Any pull requests or bug reports are welcome, please try and keep to the existing style conventions and comment any additions.