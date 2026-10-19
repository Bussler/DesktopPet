using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class MediaHeadphoneArtworkTests
{
    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void WritingPosesKeepTheirAudioAccessories(string pet)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), pet);
        foreach (var frame in assets.Definition.GetAnimation(PetAction.Write).Frames)
        {
            var art = MediaHeadphoneArtwork.Get(pet, PetAction.Write, frame);
            Assert.IsTrue(CountOpaque(Render(art.InFront), new Int32Rect(0, 0, 192, 208)) > 100,
                $"{pet} writing frame {frame.Column} must keep its headphones or earbuds.");
        }
    }

    [TestMethod]
    [DataRow("hood")]
    [DataRow("pip")]
    [DataRow("crumb")]
    [DataRow("puddle")]
    public void AttentionPosesKeepTheirAudioAccessories(string pet)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), pet);
        foreach (var frame in assets.Definition.GetAnimation(PetAction.Pet).Frames)
        {
            var art = MediaHeadphoneArtwork.Get(pet, PetAction.Pet, frame);
            Assert.IsTrue(CountOpaque(Render(art.InFront), new Int32Rect(0, 0, 192, 208)) > 100,
                $"{pet} attention frame {frame.Column} must keep its headphones or earbuds.");
        }
    }

    [TestMethod]
    [DataRow(PetAction.RunRight, 145)]
    [DataRow(PetAction.RunLeft, 39)]
    public void HoodsFarHeadbandStemStaysBehindTheCapWhenWalking(PetAction action, int farX)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        foreach (var frame in assets.Definition.GetAnimation(action).Frames.Distinct())
        {
            var art = MediaHeadphoneArtwork.Get("hood", action, frame);
            var stem = new Int32Rect(farX, 40, 8, 25);
            Assert.AreEqual(0, CountOpaque(Render(art.InFront), stem),
                $"{action} {frame}: the far stem must not cut through the cap brim.");
            Assert.IsTrue(CountOpaque(Render(art.Behind), stem) > 10,
                $"{action} {frame}: retain the far stem behind the cap.");
        }
    }

    [TestMethod]
    [DataRow(PetAction.Idle)]
    [DataRow(PetAction.RunRight)]
    [DataRow(PetAction.RunLeft)]
    public void HoodsHeadbandIsVisibleAboveTheHatInTheFrontLayer(PetAction action)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        foreach (var frame in assets.Definition.GetAnimation(action).Frames.Distinct())
        {
            var art = MediaHeadphoneArtwork.Get("hood", action, frame);
            if (action == PetAction.Idle)
                Assert.AreEqual(0, CountOpaque(Render(art.Behind), new Int32Rect(0, 0, 192, 208)),
                    "Hood's front-facing headband goes over his hat.");
            Assert.IsTrue(CountOpaque(Render(art.InFront), new Int32Rect(70, 0, 52, 40)) > 20,
                $"{action} {frame}: the curved band must be visible above the detective hat.");
            if (action is PetAction.RunRight or PetAction.RunLeft)
                Assert.IsTrue(CountOpaque(Render(art.InFront), new Int32Rect(action == PetAction.RunRight ? 123 : 39, 0, 30, 55)) > 10,
                    "The over-hat connection must continue past the crown in side views.");
        }
    }

    [TestMethod]
    [DataRow("hood", PetAction.Idle, 94)]
    [DataRow("hood", PetAction.RunRight, 89)]
    [DataRow("hood", PetAction.RunLeft, 89)]
    [DataRow("pip", PetAction.Idle, 83)]
    [DataRow("pip", PetAction.RunRight, 76)]
    [DataRow("pip", PetAction.RunLeft, 76)]
    public void CupsStayAtAnimalEarHeightInsteadOfTheCheeks(string name, PetAction action, int earBottom)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), name);
        foreach (var frame in assets.Definition.GetAnimation(action).Frames.Distinct())
        {
            var pixels = Render(MediaHeadphoneArtwork.Get(name, action, frame).InFront);
            Assert.IsTrue(CountOpaque(pixels, new Int32Rect(0, 0, 192, earBottom)) > 100);
            Assert.AreEqual(0, CountOpaque(pixels, new Int32Rect(0, earBottom, 192, 208 - earBottom)),
                $"{name} {action} {frame}: the cups extend below the animal's ear region.");
        }
    }

    [TestMethod]
    public void CrumbsInEarBudsStayCompactAndHaveNoHeadbandAcrossAllAnimations()
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "crumb");
        foreach (var animation in assets.Definition.Animations)
        foreach (var frame in animation.Frames.Distinct())
        {
            var action = Enum.Parse<PetAction>(animation.Action, true);
            var art = MediaHeadphoneArtwork.Get("crumb", action, frame);
            var pixels = CountOpaque(Render(art.InFront), new Int32Rect(0, 0, 192, 208));
            Assert.IsTrue(pixels > 10 && pixels < 400, $"{action} {frame}: in-ear buds should be small and visible, not over-ear cups.");
            Assert.AreEqual(0, CountOpaque(Render(art.Behind), new Int32Rect(0, 0, 192, 208)),
                $"{action} {frame}: Crumb's in-ear buds have no headband.");
        }
    }

    [TestMethod]
    [DataRow(PetAction.RunRight)]
    [DataRow(PetAction.RunLeft)]
    public void CrumbsProfileEarbudsRenderWithoutAnyBand(PetAction action)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "crumb");
        foreach (var frame in assets.Definition.GetAnimation(action).Frames.Distinct())
        {
            var art = MediaHeadphoneArtwork.Get("crumb", action, frame);
            Assert.AreEqual(0, CountOpaque(Render(art.Behind), new Int32Rect(0, 0, 192, 208)));
            Assert.IsTrue(CountOpaque(Render(art.InFront), new Int32Rect(0, 0, 192, 208)) > 100);
        }
    }

    [TestMethod]
    [DataRow(PetAction.RunRight, 75)]
    [DataRow(PetAction.RunLeft, 93)]
    public void CrumbsWalkingEarbudsSitAtTheLowerEarEntrance(PetAction action, int earX)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "crumb");
        foreach (var frame in assets.Definition.GetAnimation(action).Frames.Distinct())
        {
            var pixels = Render(MediaHeadphoneArtwork.Get("crumb", action, frame).InFront);
            var total = CountOpaque(pixels, new Int32Rect(0, 0, 192, 208));
            Assert.IsTrue(total > 100);
            Assert.AreEqual(total, CountOpaque(pixels, new Int32Rect(earX, 56, 24, 25)),
                $"{action} {frame}: the bud belongs lower, beside the inner ear entrance.");
        }
    }

    [TestMethod]
    [DataRow(PetAction.RunRight)]
    [DataRow(PetAction.RunLeft)]
    public void CrumbsWalkingEarbudTipPointsInward(PetAction action)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "crumb");
        foreach (var frame in assets.Definition.GetAnimation(action).Frames.Distinct())
        {
            var pixels = Render(MediaHeadphoneArtwork.Get("crumb", action, frame).InFront);
            var total = CountOpaque(pixels, new Int32Rect(0, 0, 192, 208));
            Assert.IsTrue(total > 100);
            var tipX = AverageColourX(pixels, true);
            var shellX = AverageColourX(pixels, false);
            Assert.IsTrue(action == PetAction.RunRight ? tipX > shellX + 1 : tipX < shellX - 1,
                $"{action} {frame}: the small rose tip must point toward the inner ear, with the gold shell outside.");
        }
    }

    [TestMethod]
    public void CrumbsEarbudsStayInsideThePinkEarsAcrossEveryPose()
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "crumb");
        var failures = new List<string>();
        foreach (var animation in assets.Definition.Animations)
        foreach (var frame in animation.Frames.Distinct())
        {
            var action = Enum.Parse<PetAction>(animation.Action, true);
            var buds = Render(MediaHeadphoneArtwork.Get("crumb", action, frame).InFront);
            var mouse = Render(new CroppedBitmap(assets.SpriteSheet, assets.GetFrameRectangle(frame)));
            var total = 0;
            var inside = 0;
            for (var i = 0; i < buds.Length; i += 4)
            {
                if (buds[i + 3] <= 128) continue;
                total++;
                var (b, g, r) = (mouse[i], mouse[i + 1], mouse[i + 2]);
                // Include the ear's pale pink highlights as well as its saturated shading.
                if (mouse[i + 3] > 200 && r > 160 && g > 60 && b > 60 &&
                    r > g * 1.18 && r > b * 1.12 && b > g * 0.7) inside++;
            }
            if (total <= 50 || inside < total * 0.85)
                failures.Add($"{action} {frame}: only {inside}/{total} earbud pixels sit inside the actual pink ears.");
        }
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    [DataRow("hood", PetAction.RunRight, 45, 120)]
    [DataRow("hood", PetAction.RunLeft, 103, 25)]
    [DataRow("pip", PetAction.RunRight, 78, 128)]
    [DataRow("pip", PetAction.RunLeft, 75, 14)]
    public void WalkingShowsTheVisibleCupBehindTheFace(string name, PetAction action, int earX, int faceX)
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), name);
        foreach (var frame in assets.Definition.GetAnimation(action).Frames)
        {
            var art = MediaHeadphoneArtwork.Get(name, action, frame);
            var pixels = Render(art.InFront);
            Assert.IsTrue(CountOpaque(pixels, new Int32Rect(earX, 65, 40, 65)) > 100,
                $"{name} {action} {frame}: the visible cup belongs on the back/side of the head.");
            Assert.AreEqual(0, CountOpaque(pixels, new Int32Rect(faceX, 65, 49, 65)),
                $"{name} {action} {frame}: headphones must not cover the forward-facing eye/beak/muzzle.");
        }
    }

    [TestMethod]
    public void HoodsLateHeadTurnKeepsTheCupOffTheEyeAsHeFacesRightAgain()
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "hood");
        foreach (var column in new[] { 12, 13 })
        {
            var frame = assets.Definition.GetAnimation(PetAction.LookAround).Frames[column];
            var pixels = Render(MediaHeadphoneArtwork.Get("hood", PetAction.LookAround, frame).InFront);
            Assert.IsTrue(CountOpaque(pixels, new Int32Rect(49, 85, 40, 60)) > 100,
                "These source frames face right again, so their visible cup belongs on the left/back.");
            Assert.AreEqual(0, CountOpaque(pixels, new Int32Rect(111, 95, 39, 50)));
        }
    }

    [TestMethod]
    public void PipsFrontCupsSitBesideTheHeadAndLeaveTheForeheadClear()
    {
        var assets = HoodAssets.LoadPet(Path.Combine(AppContext.BaseDirectory, "Assets"), "pip");
        foreach (var frame in assets.Definition.GetAnimation(PetAction.Idle).Frames.Distinct())
        {
            var pixels = Render(MediaHeadphoneArtwork.Get("pip", PetAction.Idle, frame).InFront);
            Assert.AreEqual(0, CountOpaque(pixels, new Int32Rect(75, 50, 85, 30)),
                "Pip's cups must not lie flat across the upper forehead.");
            Assert.IsTrue(CountOpaque(pixels, new Int32Rect(35, 65, 34, 52)) > 100);
            Assert.IsTrue(CountOpaque(pixels, new Int32Rect(155, 65, 35, 52)) > 100);
        }
    }

    private static byte[] Render(ImageSource image)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) drawing.DrawImage(image, new Rect(0, 0, 192, 208));
        var bitmap = new RenderTargetBitmap(192, 208, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[192 * 208 * 4];
        bitmap.CopyPixels(pixels, 192 * 4, 0);
        return pixels;
    }

    private static int CountOpaque(byte[] pixels, Int32Rect region)
    {
        var count = 0;
        for (var y = region.Y; y < region.Y + region.Height; y++)
        for (var x = region.X; x < region.X + region.Width; x++)
            if (pixels[(y * 192 + x) * 4 + 3] > 16) count++;
        return count;
    }

    private static double AverageColourX(byte[] pixels, bool rose)
    {
        var count = 0;
        var sum = 0;
        for (var y = 0; y < 208; y++)
        for (var x = 0; x < 192; x++)
        {
            var offset = (y * 192 + x) * 4;
            var (b, g, r) = (pixels[offset], pixels[offset + 1], pixels[offset + 2]);
            if (pixels[offset + 3] > 128 && (rose ? b > g * 0.8 && r > g * 1.15 : g > 80 && b < g * 0.7))
            {
                count++;
                sum += x;
            }
        }
        Assert.IsTrue(count > 5, "The earbud must show both its rose tip and gold shell.");
        return (double)sum / count;
    }
}
