using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>FR-085: one vector master of the NetPrints mark with its exports, no stray copies of the logo, and the third-party notices file.</summary>
    public class BrandAssetTests
    {
        private static readonly int[] PngSizes = [16, 24, 32, 48, 64, 128, 256];

        private static readonly int[] IcoSizes = [16, 32, 48, 256];

        private static string Root { get; } = SampleProjectFactory.FindRepositoryRoot();

        private static string PathOf(string relative) => Path.Combine(Root, relative);

        private static (int Width, int Height) PngSize(string relative)
        {
            byte[] header = File.ReadAllBytes(PathOf(relative));
            Assert.True(header.Length > 24, relative);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, header[..4]);
            return (ReadBigEndian(header, 16), ReadBigEndian(header, 20));
        }

        private static int ReadBigEndian(byte[] bytes, int offset) =>
            (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

        private static int[] IcoFrameSizes(string relative)
        {
            byte[] ico = File.ReadAllBytes(PathOf(relative));
            Assert.Equal(0, BitConverter.ToUInt16(ico, 0));
            Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
            int count = BitConverter.ToUInt16(ico, 4);
            return [.. Enumerable.Range(0, count).Select(index => ico[6 + (index * 16)] == 0 ? 256 : ico[6 + (index * 16)]).Order()];
        }

        [Fact]
        public void TheSvgMasterHasASquareViewBox()
        {
            XElement root = XDocument.Load(PathOf("assets/brand/netprints-mark.svg")).Root ?? throw new InvalidOperationException("The SVG has no root element.");
            double[] box = [.. root.Attribute("viewBox")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)) ?? []];

            Assert.Equal(4, box.Length);
            Assert.Equal(box[2], box[3]);
            Assert.True(box[2] > 0);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(24)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(256)]
        public void EachPngExportHasItsPixelSize(int size) =>
            Assert.Equal((size, size), PngSize($"assets/brand/netprints-mark-{size}.png"));

        [Fact]
        public void TheExportSizesAreTheDocumentedSeven()
        {
            int[] found = [.. Directory.EnumerateFiles(PathOf("assets/brand"), "netprints-mark-*.png").Select(path => int.Parse(Path.GetFileNameWithoutExtension(path)["netprints-mark-".Length..], System.Globalization.CultureInfo.InvariantCulture)).Order()];

            Assert.Equal(PngSizes, found);
        }

        [Fact]
        public void TheDesktopIconHoldsTheFourFramesAndTheEditorCopyIsIdentical()
        {
            int[] frames = IcoFrameSizes("src/NetPrints.Desktop/NetPrintsLogo.ico");

            Assert.Equal(IcoSizes, frames);
            Assert.Equal(File.ReadAllBytes(PathOf("src/NetPrints.Desktop/NetPrintsLogo.ico")), File.ReadAllBytes(PathOf("src/NetPrints.Editor/Assets/NetPrintsLogo.ico")));
        }

        [Fact]
        public void TheNuGetIconIsThe128PixelExport() =>
            Assert.Equal(File.ReadAllBytes(PathOf("assets/brand/netprints-mark-128.png")), File.ReadAllBytes(PathOf("assets/icons/netprints-icon.png")));

        [Theory]
        [InlineData("src/NetPrints.Editor/Assets/NetPrintsLogo.png")]
        [InlineData("website/static/img/logo.png")]
        public void NoCopyOfTheLogoPngRemains(string relative) =>
            Assert.False(File.Exists(PathOf(relative)), $"{relative} must be removed: the mark has one master.");

        [Fact]
        public void TheThirdPartyNoticesNameEveryBundledAsset()
        {
            string notices = File.ReadAllText(PathOf("THIRD-PARTY-NOTICES.md"));

            string[] required =
            [
                "Material Design Icons", "Pictogrammers", "Apache-2.0", "Material.Icons.Avalonia", "Semi.Avalonia", "Irihi.Ursa", "Irihi.Ursa.Themes.Semi",
                "Cascadia Mono", "Inter", "SIL OFL 1.1", "Copyright (c) 2022 IRIHI Technology", "Copyright (c) 2025 .NET Foundation and Contributors",
                "Copyright (c) 2019 - Present, Microsoft Corporation", "The Inter Project Authors", "Apache License", "Version 2.0, January 2004",
                "ChevronDown", "DockWindow", "WindowMinimize",
            ];
            string[] missing = [.. required.Where(text => !notices.Contains(text, StringComparison.Ordinal))];
            Assert.Empty(missing);
        }

        [Fact]
        public void TheDesktopProjectCopiesTheNoticesToItsOutputAndPublishDirectories()
        {
            string project = File.ReadAllText(PathOf("src/NetPrints.Desktop/NetPrints.Desktop.csproj"));

            Assert.Contains("THIRD-PARTY-NOTICES.md", project, StringComparison.Ordinal);
            Assert.Contains("CopyToOutputDirectory", project, StringComparison.Ordinal);
            Assert.Contains("CopyToPublishDirectory", project, StringComparison.Ordinal);
        }
    }
}
