﻿using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Telegram.Bot.Tests.Integ.Framework.XunitExtensions;

public class XunitTestFrameworkWithAssemblyFixture(IMessageSink messageSink) : XunitTestFramework(messageSink)
{
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        => new XunitTestFrameworkExecutorWithAssemblyFixture(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink
        );
}
