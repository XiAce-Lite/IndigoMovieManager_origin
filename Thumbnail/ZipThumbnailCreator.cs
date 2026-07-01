using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using OpenCvSharp;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ZipThumbnailCreator
    {
        public static async Task<ThumbnailCreateResult> TryCreateAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            IReadOnlyList<string> entryNames,
            CancellationToken cts = default)
        {
            if (ctx == null || thumbInfo == null || entryNames == null || entryNames.Count == 0)
            {
                return ThumbnailCreateResult.Failed("zip: no images");
            }

            List<string> panelPaths = [];
            try
            {
                await Task.Run(() =>
                {
                    using ZipArchive archive = ZipFile.OpenRead(ctx.MovieFullPath);
                    for (int panel = 0; panel < thumbInfo.ThumbCounts; panel++)
                    {
                        cts.ThrowIfCancellationRequested();

                        int imageIndex = thumbInfo.ThumbSec.Count > panel
                            ? thumbInfo.ThumbSec[panel]
                            : 0;
                        if (imageIndex < 0 || imageIndex >= entryNames.Count)
                        {
                            imageIndex = Math.Min(Math.Max(imageIndex, 0), entryNames.Count - 1);
                        }

                        string entryName = entryNames[imageIndex];
                        string panelPath = Path.Combine(
                            ctx.TempPath,
                            $"{ctx.TempFileBody}_p{panel}.jpg");

                        if (!TryRenderEntryToJpeg(
                                archive,
                                entryName,
                                panelPath,
                                ctx.TabInfo.Width,
                                ctx.TabInfo.Height,
                                cts))
                        {
                            throw new InvalidOperationException($"zip: decode failed {entryName}");
                        }

                        panelPaths.Add(panelPath);
                    }
                }, cts).ConfigureAwait(false);

                if (panelPaths.Count != thumbInfo.ThumbCounts)
                {
                    return ThumbnailCreateResult.Failed("zip: incomplete panels");
                }

                using Bitmap bmp = ConcatImages(panelPaths, ctx.TabInfo.Columns, ctx.TabInfo.Rows);
                if (bmp == null)
                {
                    return ThumbnailCreateResult.Failed("zip: concat failed");
                }

                if (File.Exists(ctx.SaveThumbFileName))
                {
                    File.Delete(ctx.SaveThumbFileName);
                }

                bmp.Save(ctx.SaveThumbFileName, ImageFormat.Jpeg);
                ThumbnailMetadataWriter.AppendMetadata(ctx.SaveThumbFileName, thumbInfo);
                return ThumbnailCreateResult.Succeeded(panelPaths, "ZIP", "");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ThumbnailCreateResult.Failed(ex.Message);
            }
            finally
            {
                foreach (string path in panelPaths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static bool TryRenderEntryToJpeg(
            ZipArchive archive,
            string entryName,
            string destPath,
            int targetWidth,
            int targetHeight,
            CancellationToken cts)
        {
            ZipArchiveEntry entry = ZipArchiveEntryResolver.FindEntry(archive, entryName);
            if (entry == null)
            {
                return false;
            }

            ResolvePanelSize(targetWidth, targetHeight, out int panelWidth, out int panelHeight);

            using Stream stream = entry.Open();
            if (ZipImageCatalog.IsWebpEntry(entryName))
            {
                return TryRenderWebpStreamToJpeg(stream, destPath, panelWidth, panelHeight, cts);
            }

            return TryRenderRasterStreamToJpeg(stream, destPath, panelWidth, panelHeight, cts);
        }

        private static bool TryRenderWebpStreamToJpeg(
            Stream stream,
            string destPath,
            int panelWidth,
            int panelHeight,
            CancellationToken cts)
        {
            byte[] data = ReadAllBytes(stream);
            return ZipWebpDecoder.TryRenderToLetterboxedJpeg(data, destPath, panelWidth, panelHeight, cts);
        }

        private static bool TryRenderRasterStreamToJpeg(
            Stream stream,
            string destPath,
            int panelWidth,
            int panelHeight,
            CancellationToken cts)
        {
            byte[] data = ReadAllBytes(stream);
            if (data.Length == 0)
            {
                return false;
            }

            if (TryRenderGdiBytesToJpeg(data, destPath, panelWidth, panelHeight))
            {
                return true;
            }

            if (TryRenderOpenCvBytesToJpeg(data, destPath, panelWidth, panelHeight))
            {
                return true;
            }

            return false;
        }

        private static bool TryRenderGdiBytesToJpeg(byte[] data, string destPath, int panelWidth, int panelHeight)
        {
            try
            {
                using var stream = new MemoryStream(data, writable: false);
                using Image source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                using Bitmap panel = new(panelWidth, panelHeight, PixelFormat.Format24bppRgb);
                using (Graphics graphics = Graphics.FromImage(panel))
                {
                    DrawLetterboxedImage(graphics, source, panelWidth, panelHeight);
                }

                panel.Save(destPath, ImageFormat.Jpeg);
                return File.Exists(destPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryRenderOpenCvBytesToJpeg(byte[] data, string destPath, int panelWidth, int panelHeight)
        {
            try
            {
                using Mat source = Cv2.ImDecode(data, ImreadModes.Color);
                if (source.Empty())
                {
                    return false;
                }

                return ZipWebpDecoder.RenderMatToLetterboxedJpeg(source, destPath, panelWidth, panelHeight);
            }
            catch
            {
                return false;
            }
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null)
            {
                return [];
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        private static void ResolvePanelSize(int targetWidth, int targetHeight, out int panelWidth, out int panelHeight)
        {
            panelWidth = targetWidth > 0 ? targetWidth : 160;
            panelHeight = targetHeight > 0 ? targetHeight : 120;

            if (panelWidth < 1)
            {
                panelWidth = 1;
            }

            if (panelHeight < 1)
            {
                panelHeight = 1;
            }
        }

        private static void DrawLetterboxedImage(Graphics graphics, Image source, int panelWidth, int panelHeight)
        {
            graphics.Clear(Color.Black);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            ComputeLetterboxDrawSize(source.Width, source.Height, panelWidth, panelHeight, out int drawWidth, out int drawHeight, out int drawX, out int drawY);
            graphics.DrawImage(source, drawX, drawY, drawWidth, drawHeight);
        }

        private static void ComputeLetterboxDrawSize(
            int sourceWidth,
            int sourceHeight,
            int panelWidth,
            int panelHeight,
            out int drawWidth,
            out int drawHeight,
            out int drawX,
            out int drawY)
        {
            double scaleX = (double)panelWidth / sourceWidth;
            double scaleY = (double)panelHeight / sourceHeight;
            double scale = Math.Min(scaleX, scaleY);

            drawWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            drawHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            drawX = (panelWidth - drawWidth) / 2;
            drawY = (panelHeight - drawHeight) / 2;
        }
    }
}
