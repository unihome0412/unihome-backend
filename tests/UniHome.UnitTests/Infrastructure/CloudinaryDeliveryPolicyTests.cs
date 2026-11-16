using UniHome.Infrastructure.Storage;

namespace UniHome.UnitTests.Infrastructure;

public sealed class CloudinaryDeliveryPolicyTests
{
    [Fact]
    public void HttpsYHostCloudinary_DebenAceptarse()
    {
        var uri = CloudinaryPhotoClient.CreateTrustedDeliveryUri(
            "https://res.cloudinary.com/demo/image/authenticated/s--firma--/foto.png");

        Assert.Equal("res.cloudinary.com", uri.Host);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }

    [Theory]
    [InlineData("http://res.cloudinary.com/demo/image/authenticated/foto.png")]
    [InlineData("https://evil.example/image/authenticated/foto.png")]
    [InlineData("https://res.cloudinary.com.evil.example/foto.png")]
    [InlineData("https://api.cloudinary.com/v1_1/demo/image/upload")]
    [InlineData("not-a-uri")]
    public void DestinosNoConfiables_DebenRechazarse(string url)
    {
        Assert.Throws<CloudinaryProviderException>(() =>
            CloudinaryPhotoClient.CreateTrustedDeliveryUri(url));
    }
}
