/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

public sealed class CredentialStoreSelectorTests
{
    [Fact]
    public void TheFileStoreIsSelectedWhileItIsTheOnlyBackend()
    {
        var file = new FileCredentialStore(new XpingHome(Path.Combine(Path.GetTempPath(), "unused")), Serializer);
        var selector = new CredentialStoreSelector(file);

        CredentialStores stores = selector.Select();

        Assert.Same(file, stores.Selected);
        Assert.Equal([file], stores.ReadOrder);
        Assert.Null(stores.FallbackReason);
        Assert.Same(stores, selector.Select());
    }
}
