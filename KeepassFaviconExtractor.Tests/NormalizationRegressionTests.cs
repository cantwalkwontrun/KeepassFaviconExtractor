using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FaviconExtractor
{
    [TestClass]
    public class NormalizationRegressionTests
    {
        private const string SvgStart = "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'>";

        [DataTestMethod]
        [DataRow("<use href='https://example.test/external.svg#icon'/>")]
        [DataRow("<use href='file:///C:/synthetic/icon.svg#icon'/>")]
        [DataRow("<use href='\\\\synthetic-share\\icon.svg#icon'/>")]
        [DataRow("<rect width='32' height='32' fill='url(https://example.test/paint)'/>")]
        [DataRow("<style>@import 'https://example.test/style.css';</style>")]
        [DataRow("<style>rect { fill: \\75rl(https://example.test/paint); }</style>")]
        [DataRow("<image href='data:image/png;base64,synthetic'/>")]
        public void Svg_RejectsExternalOrEmbeddedResourcesBeforeRendering(string content)
        {
            Assert.ThrowsException<InvalidOperationException>(() => Normalize(SvgStart + content + "</svg>"));
        }

        [TestMethod]
        public void Svg_RejectsDtdAndStylesheetInstructions()
        {
            string dtd = "<!DOCTYPE svg [<!ENTITY external SYSTEM 'file:///C:/synthetic/missing.txt'>]>";
            var error = Assert.ThrowsException<InvalidOperationException>(() => Normalize(dtd + SvgStart + "&external;</svg>"));
            StringAssert.Contains(error.Message, "DTD");
            Assert.ThrowsException<InvalidOperationException>(() => Normalize("<?xml-stylesheet href='https://example.test/style.css'?>" + SvgStart + "</svg>"));
        }

        [TestMethod]
        public void Svg_RejectsDeepAndExcessivelyComplexDocuments()
        {
            var nested = new StringBuilder(SvgStart);
            for (int i = 0; i < 80; i++) nested.Append("<g>");
            nested.Append("<circle cx='16' cy='16' r='10'/>");
            for (int i = 0; i < 80; i++) nested.Append("</g>");
            nested.Append("</svg>");
            Assert.ThrowsException<InvalidOperationException>(() => Normalize(nested.ToString()));
            var many = new StringBuilder(SvgStart);
            for (int i = 0; i < 4200; i++) many.Append("<path d='M0 0 L1 1'/>");
            many.Append("</svg>");
            Assert.ThrowsException<InvalidOperationException>(() => Normalize(many.ToString()));
        }

        [TestMethod]
        public void Svg_ValidXmlDeclarationBomAndLocalPaintReferenceStillRender()
        {
            const string svg = "\uFEFF<?xml version='1.0'?>" + SvgStart +
                "<defs><linearGradient id='paint'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs>" +
                "<rect width='32' height='32' fill='url(#paint)'/></svg>";
            AssertPng(Normalize(svg));
        }

        [TestMethod]
        public void Svg_RejectsCyclicAndExponentialLocalReferences()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Normalize(SvgStart + "<g id='loop'><use href='#loop'/></g></svg>"));
            var graph = new StringBuilder(SvgStart + "<defs><g id='g0'><rect width='32' height='32'/></g>");
            for (int i = 1; i <= 30; i++)
                graph.Append("<g id='g" + i + "'><use href='#g" + (i - 1) + "'/><use href='#g" + (i - 1) + "'/></g>");
            graph.Append("</defs><use href='#g30'/></svg>");
            var error = Assert.ThrowsException<InvalidOperationException>(() => Normalize(graph.ToString()));
            StringAssert.Contains(error.Message, "complex");
        }

        [TestMethod]
        public void Svg_OrdinaryLocalShapeReuseStillRenders()
        {
            AssertPng(Normalize(SvgStart + "<defs><rect id='shape' width='32' height='32' fill='blue'/></defs><use href='#shape'/></svg>"));
        }

        [TestMethod]
        public void Svg_LargeDeclaredDimensionsRenderIntoBoundedCanvas()
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='100000' height='100000'><rect width='100000' height='100000' fill='red'/></svg>";
            byte[] bytes = Normalize(svg);
            using (var stream = new MemoryStream(bytes))
            using (var image = Image.FromStream(stream)) Assert.AreEqual(128, image.Width);
        }

        [TestMethod]
        public void HtmlWithInlineSvgIsRejectedButMislabeledPngIsAccepted()
        {
            var error = Assert.ThrowsException<InvalidOperationException>(() => IconNormalizer.NormalizeToPng(
                Encoding.UTF8.GetBytes("<html><body>Sign in<svg></svg></body></html>"), "application/octet-stream", "https://example.test/icon.svg"));
            StringAssert.Contains(error.Message, "HTML");
            using (var bitmap = new Bitmap(32, 32))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                AssertPng(IconNormalizer.NormalizeToPng(stream.ToArray(), "text/html", "https://example.test/icon.svg"));
                AssertPng(IconNormalizer.NormalizeToPng(stream.ToArray(), "image/svg+xml", "https://example.test/icon.svg"));
            }
        }

        [TestMethod]
        public void StaRenderer_TimeoutRetainsSlotAndDisposesOneLateResult()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var resource = new TrackedResource();
                var first = Task.Run(() => StaRenderOperation.Run(() => { entered.Set(); release.Wait(); return resource; }, CancellationToken.None, TimeSpan.FromMilliseconds(150)));
                try
                {
                    Assert.IsTrue(entered.Wait(2000));
                    Assert.ThrowsException<TimeoutException>(() => first.GetAwaiter().GetResult());
                    bool secondRan = false;
                    Assert.ThrowsException<TimeoutException>(() => StaRenderOperation.Run(() => { secondRan = true; return 1; }, CancellationToken.None, TimeSpan.FromMilliseconds(80)));
                    Assert.IsFalse(secondRan);
                }
                finally { release.Set(); }
                Assert.IsTrue(SpinWait.SpinUntil(() => resource.Disposals == 1, 2000));
            }
        }

        [TestMethod]
        public void StaRenderer_CancellationDisposesLateResult()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var cancellation = new CancellationTokenSource())
            {
                var resource = new TrackedResource();
                var first = Task.Run(() => StaRenderOperation.Run(() => { entered.Set(); release.Wait(); return resource; }, cancellation.Token, TimeSpan.FromSeconds(3)));
                try
                {
                    Assert.IsTrue(entered.Wait(2000)); cancellation.Cancel();
                    Assert.ThrowsException<OperationCanceledException>(() => first.GetAwaiter().GetResult());
                }
                finally { release.Set(); }
                Assert.IsTrue(SpinWait.SpinUntil(() => resource.Disposals == 1, 2000));
            }
        }

        [TestMethod]
        public void StaRenderer_StaCallerStillReceivesDeadline()
        {
            using (var release = new ManualResetEventSlim())
            {
                Exception failure = null;
                var caller = new Thread(() =>
                {
                    try { StaRenderOperation.Run(() => { release.Wait(); return 1; }, CancellationToken.None, TimeSpan.FromMilliseconds(100)); }
                    catch (Exception error) { failure = error; }
                });
                caller.IsBackground = true; caller.SetApartmentState(ApartmentState.STA); caller.Start();
                try { Assert.IsTrue(caller.Join(2000)); Assert.IsInstanceOfType(failure, typeof(TimeoutException)); }
                finally { release.Set(); caller.Join(2000); }
            }
        }

        [TestMethod]
        public void StaRenderer_PreservesActualFailureAndUsesStaWorker()
        {
            var error = Assert.ThrowsException<InvalidOperationException>(() => StaRenderOperation.Run<int>(
                () => { throw new InvalidOperationException("synthetic conversion detail"); }, CancellationToken.None, TimeSpan.FromSeconds(3)));
            Assert.AreEqual("synthetic conversion detail", error.Message);
            Assert.AreEqual(ApartmentState.STA, StaRenderOperation.Run(() => Thread.CurrentThread.GetApartmentState(), CancellationToken.None, TimeSpan.FromSeconds(3)));
        }

        private static byte[] Normalize(string svg) => IconNormalizer.NormalizeToPng(Encoding.UTF8.GetBytes(svg), "image/svg+xml", "https://example.test/icon.svg");
        private static void AssertPng(byte[] bytes) { Assert.IsTrue(bytes.Length > 8 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71); }
        private sealed class TrackedResource : IDisposable
        {
            internal int Disposals;
            public void Dispose() { Interlocked.Increment(ref Disposals); }
        }
    }
}
