using OuttaContext.Infrastructure.YtDlp;

namespace OuttaContext.Infrastructure.Tests.YtDlp;

public sealed class SystemEnvironmentInspectorTests
{
    [Fact]
    public void ParseImpersonationClients_TargetTable_ReturnsDistinctLowerCaseClients()
    {
        const string Output = """
            [info] Available impersonate targets
            Client          OS           Source
            --------------------------------------
            Chrome-133      Macos-15     curl_cffi
            Chrome-136      Macos-15     curl_cffi
            Firefox-135     Macos-14     curl_cffi
            Safari-18.0     Ios-18.0     curl_cffi
            """;

        SystemEnvironmentInspector.ParseImpersonationClients(Output).ShouldBe(["chrome", "firefox", "safari"]);
    }

    [Fact]
    public void ParseImpersonationClients_NoCurlCffi_ReturnsNothing()
    {
        SystemEnvironmentInspector.ParseImpersonationClients("Chrome  -  curl_cffi (unavailable)").ShouldBeEmpty();
    }
}
