using System.Diagnostics;

using System.IO;



namespace IndigoMovieManager.Services

{

    internal static class ZipImageViewerLauncher

    {

        public static (string Program, string Param) ResolveViewer(

            string dbPlayerPrg,

            string dbPlayerParam,

            string defaultZipViewerPath,

            string defaultZipViewerParam)

        {

            string program = string.IsNullOrWhiteSpace(dbPlayerPrg) ? defaultZipViewerPath : dbPlayerPrg;

            string param = string.IsNullOrWhiteSpace(dbPlayerParam) ? defaultZipViewerParam : dbPlayerParam;

            return (program, param);

        }



        public static bool TryOpen(

            string zipPath,

            string dbPlayerPrg,

            string dbPlayerParam,

            string defaultZipViewerPath,

            string defaultZipViewerParam)

        {

            (string viewerProgram, string viewerParam) = ResolveViewer(

                dbPlayerPrg,

                dbPlayerParam,

                defaultZipViewerPath,

                defaultZipViewerParam);

            return TryOpen(zipPath, viewerProgram, viewerParam);

        }



        public static bool TryOpen(string zipPath, string viewerProgram, string viewerParam)

        {

            zipPath = MediaPathNormalizer.Normalize(zipPath);

            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))

            {

                return false;

            }



            try

            {

                if (!string.IsNullOrWhiteSpace(viewerProgram) && File.Exists(viewerProgram))

                {

                    string args = BuildArguments(zipPath, viewerParam);

                    using Process process = new();

                    process.StartInfo.FileName = viewerProgram;

                    process.StartInfo.Arguments = args;

                    process.StartInfo.UseShellExecute = false;

                    process.Start();

                    return true;

                }



                using Process shell = new();

                shell.StartInfo.FileName = zipPath;

                shell.StartInfo.UseShellExecute = true;

                shell.Start();

                return true;

            }

            catch

            {

                return false;

            }

        }



        private static string BuildArguments(string zipPath, string viewerParam)

        {

            string quotedPath = $"\"{zipPath}\"";

            string args = viewerParam ?? "";

            if (!string.IsNullOrEmpty(args))

            {

                args = args.Replace("<file>", zipPath, StringComparison.OrdinalIgnoreCase);

            }



            if (args.Contains(zipPath, StringComparison.OrdinalIgnoreCase))

            {

                return args;

            }



            return string.IsNullOrWhiteSpace(args)

                ? quotedPath

                : $"{args} {quotedPath}";

        }

    }

}


