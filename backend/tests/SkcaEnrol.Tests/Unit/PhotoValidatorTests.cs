using Microsoft.AspNetCore.Http;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Tests.Unit;

public class PhotoValidatorTests
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0 };

    private static IFormFile MakeFile(byte[] content, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "photo", "photo")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public void Accepts_a_real_png()
    {
        Assert.Null(PhotoValidator.Validate(MakeFile(PngHeader, "image/png")));
    }

    [Fact]
    public void Accepts_a_real_jpeg()
    {
        Assert.Null(PhotoValidator.Validate(MakeFile(JpegHeader, "image/jpeg")));
    }

    [Fact]
    public void Rejects_a_disallowed_content_type()
    {
        Assert.NotNull(PhotoValidator.Validate(MakeFile(PngHeader, "image/gif")));
    }

    [Fact]
    public void Rejects_a_file_that_lies_about_being_an_image()
    {
        // Claims to be PNG, but the bytes are plain text (e.g. a renamed script).
        var text = "<script>alert(1)</script>"u8.ToArray();
        Assert.NotNull(PhotoValidator.Validate(MakeFile(text, "image/png")));
    }

    [Fact]
    public void Rejects_files_over_2MB()
    {
        var big = new byte[PhotoValidator.MaxBytes + 1];
        PngHeader.CopyTo(big, 0);
        Assert.Equal("Photo must be 2 MB or smaller.", PhotoValidator.Validate(MakeFile(big, "image/png")));
    }
}
