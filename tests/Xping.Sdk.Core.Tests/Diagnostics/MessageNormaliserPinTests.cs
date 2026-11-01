/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Diagnostics;

/// <summary>
/// Pins <c>errorMessageHash</c> for fixed messages. The hash is the failure signature Xping Cloud and
/// the CLI match on (cloud-data-in-cli contract §5.1), and the normaliser behind it is frozen by
/// contract §5.2: a change makes every stored hash in Cloud and in local session files stop matching
/// new ones. If one of these fails, the change is a contract amendment first, not a test update.
/// </summary>
/// <remarks>
/// The expected values were computed outside .NET (<c>printf %s '…' | shasum -a 256</c>) from the
/// normalised form named in each row's comment, so the test does not bless whatever the code
/// produces today.
/// </remarks>
public sealed class MessageNormaliserPinTests
{
    [Theory]
    // run <guid> failed
    [InlineData("Run 3F2504E0-4F89-11D3-9A0C-0305E82C3301 failed",
        "369a8ad398ad8705620b0e3793d4338ad85360b9216ded7215da30ece6db8230")]
    // get <uri> timed out
    [InlineData("GET https://api.example.com/v2/orders?id=7 timed out",
        "fc1e2bcf7f6df9ae80a836ef5b411c48dd2123abef0aa3244a90a8b9512bbd00")]
    // could not open <path>
    [InlineData(@"could not open C:\Users\adrian\app\config.json",
        "a96190c1a38c8ec56bd3084b1b90c776525dd18ba3d1f119e306974bb0fe3bf2")]
    // expected <time> but got <time>
    [InlineData("expected 2026-08-10T20:08:30Z but got 2026-08-11",
        "85b9422e19ce237eabc94d24d13a382f606b5782796d403a6463f4e204d03129")]
    // handle <hex> was closed
    [InlineData("Handle 0xDEADBEEF was closed",
        "3733c0203cd2d3863c4eb35c45258e6a08c9f5c1a89784f1545dafc657e9c722")]
    // expected <num> but got <num>: line endings and whitespace runs collapse first
    [InlineData("Expected 42\r\n   but got 43.75",
        "37f1c722fb043b14d8134e909b81aabff2122f71ff0e56109084422ddd94cb4b")]
    // nullable`1 was null: a digit that belongs to a name stays
    [InlineData("Nullable`1 was null",
        "a522f9241bce483dee6c2ceed1e38821a0e4eb7ee5b64e1aa5ffca3a4843aea6")]
    // expected 'Local' but was 'Remote': a quoted literal without digits keeps its case
    [InlineData("Expected 'Local'\r\n  but   was 'Remote'",
        "0290b9c80ac6bbc5b9a901454a967d1597d18b19466de34c13e99bedff9643da")]
    // key 'order <num>' missing: a quoted literal with a digit goes through the rules
    [InlineData("Key 'Order 42' missing",
        "8878190b759ac2a9c4e90940c5ffdc69d6e8735f36794ed0467032ec5e5d9793")]
    // could not find file '<path>'.: a quoted literal that is a whole path, space included
    [InlineData("Could not find file '/Users/a b/x.json'.",
        "ef0cecc07cb7b031fcf33b3f079c550aa4daf2c265bca22e5bf2f956b44c60cd")]
    // get /orders failed: one segment is a route, not a path
    [InlineData("GET /orders failed",
        "e982f950a81d5871334d6dfe5e629a20af05adc84e6b6448f0a225ad22f98f33")]
    // digest <hex> mismatched: a bare run of 16 or more hex digits
    [InlineData("digest 7e5e7382baaeaa09 mismatched",
        "5d1aa2608ffb37bd1b6d1027c5476b472365bb7df52b29dc3659c41dc5976927")]
    // due <time> at <time>: a d/m/y date and a clock reading
    [InlineData("due 1/2/2026 at 10:15:30",
        "f86f316ca1851790d45de5137b6ec445de73ea29eaaa9e69a2f38b2d96cc27d9")]
    public void TheHashOfAFixedMessageNeverMoves(string message, string expected) =>
        Assert.Equal(expected, BuildGenerator().GenerateErrorMessageHash(message));

    [Fact]
    public void OnlyTheFirstMaxLengthCharactersAreHashed() =>
        // 4096 × "z"; not a letter from a to f, or the run is <hex>
        Assert.Equal(
            "80f1830e2934a1c06ceb7512d00bb936a9437c80411da172c1a274238b974795",
            BuildGenerator().GenerateErrorMessageHash(new string('Z', 5000)));

    private static ITestIdentityGenerator BuildGenerator() =>
        ServiceHelper.CreateCollectorServices().BuildServiceProvider().GetRequiredService<ITestIdentityGenerator>();
}
