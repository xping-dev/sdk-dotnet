/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Extensions;

using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.UnitTests.Extensions;

internal class TestContextExtensionTests
{
    [Test]
    public void GetNonSerializablePropertyBagValueThrowsArugmentNullExceptionWhenContextIsNull()
    {
        // Arrange
        TestContext context = null!;

        // Assert
        Assert.Throws<ArgumentNullException>(() => 
            context.GetNonSerializablePropertyBagValue<object>(new PropertyBagKey("key")));
    }
}
