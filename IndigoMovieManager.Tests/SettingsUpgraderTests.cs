using IndigoMovieManager.Properties;
using Xunit;

namespace IndigoMovieManager.Tests;

public class SettingsUpgraderTests
{
    [Fact]
    public void TryGetProfileDirectory_returns_parent_of_version_folder()
    {
        string profileExpected = Path.Combine(
            Path.GetTempPath(),
            "IndigoMovieManager",
            "IndigoMovieManager_Path_abc");
        string path = Path.Combine(profileExpected, "1.0.0.78", "user.config");

        string profile = SettingsUpgrader.TryGetProfileDirectory(path);

        Assert.Equal(profileExpected, profile);
    }

    [Fact]
    public void TryGetProfileDirectory_returns_null_for_empty()
    {
        Assert.Null(SettingsUpgrader.TryGetProfileDirectory(null));
        Assert.Null(SettingsUpgrader.TryGetProfileDirectory(""));
    }

    [Fact]
    public void HasDefaultWindow_detects_default_location_and_size()
    {
        string text = """
            <setting name="MainLocation" serializeAs="String">
                <value>10, 10</value>
            </setting>
            <setting name="MainSize" serializeAs="String">
                <value>800, 600</value>
            </setting>
            """;

        Assert.True(SettingsUpgrader.HasDefaultWindow(text));
    }

    [Fact]
    public void LooksLikeStoredDefaults_true_for_empty_last_doc_and_default_window()
    {
        string dir = CreateTempProfile();
        try
        {
            string path = Path.Combine(dir, "1.0.0.76", "user.config");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <userSettings>
                        <IndigoMovieManager.Properties.Settings>
                            <setting name="MainLocation" serializeAs="String">
                                <value>10, 10</value>
                            </setting>
                            <setting name="MainSize" serializeAs="String">
                                <value>800, 600</value>
                            </setting>
                            <setting name="LastDoc" serializeAs="String">
                                <value />
                            </setting>
                        </IndigoMovieManager.Properties.Settings>
                    </userSettings>
                </configuration>
                """);

            Assert.True(SettingsUpgrader.LooksLikeStoredDefaults(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LooksLikeStoredDefaults_false_when_last_doc_present()
    {
        string dir = CreateTempProfile();
        try
        {
            string path = Path.Combine(dir, "1.0.0.75", "user.config");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <userSettings>
                        <IndigoMovieManager.Properties.Settings>
                            <setting name="MainLocation" serializeAs="String">
                                <value>682, 3</value>
                            </setting>
                            <setting name="MainSize" serializeAs="String">
                                <value>1371, 1104</value>
                            </setting>
                            <setting name="LastDoc" serializeAs="String">
                                <value>F:\WhiteBrowser\Xドライブ用.wb</value>
                            </setting>
                        </IndigoMovieManager.Properties.Settings>
                    </userSettings>
                </configuration>
                """);

            Assert.False(SettingsUpgrader.LooksLikeStoredDefaults(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FindBestPreviousUserConfig_prefers_rich_previous_over_empty()
    {
        string profile = CreateTempProfile();
        try
        {
            string empty76 = Path.Combine(profile, "1.0.0.76", "user.config");
            string good75 = Path.Combine(profile, "1.0.0.75", "user.config");
            Directory.CreateDirectory(Path.GetDirectoryName(empty76)!);
            Directory.CreateDirectory(Path.GetDirectoryName(good75)!);

            File.WriteAllText(empty76, """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <userSettings>
                        <IndigoMovieManager.Properties.Settings>
                            <setting name="MainLocation" serializeAs="String">
                                <value>10, 10</value>
                            </setting>
                            <setting name="MainSize" serializeAs="String">
                                <value>800, 600</value>
                            </setting>
                            <setting name="LastDoc" serializeAs="String">
                                <value />
                            </setting>
                        </IndigoMovieManager.Properties.Settings>
                    </userSettings>
                </configuration>
                """);

            // 1500 bytes 未満は候補外なので、実データ相当の長さにする。
            string rich = """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <userSettings>
                        <IndigoMovieManager.Properties.Settings>
                            <setting name="MainLocation" serializeAs="String">
                                <value>682, 3</value>
                            </setting>
                            <setting name="MainSize" serializeAs="String">
                                <value>1371, 1104</value>
                            </setting>
                            <setting name="LastDoc" serializeAs="String">
                                <value>F:\WhiteBrowser\Xドライブ用.wb</value>
                            </setting>
                            <setting name="RecentFiles" serializeAs="Xml">
                                <value>
                                    <ArrayOfString xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                                        xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                                        <string>F:\WhiteBrowser\Kuniko.wb</string>
                                        <string>I:\Secret\MaU\m.wb</string>
                                    </ArrayOfString>
                                </value>
                            </setting>
                            <setting name="AutoOpen" serializeAs="String">
                                <value>False</value>
                            </setting>
                            <setting name="ConfirmExit" serializeAs="String">
                                <value>False</value>
                            </setting>
                            <setting name="DefaultPlayerPath" serializeAs="String">
                                <value>C:\Program Files\MPC-BE x64\mpc-be64.exe</value>
                            </setting>
                            <setting name="DefaultPlayerParam" serializeAs="String">
                                <value>/start &lt;ms&gt;</value>
                            </setting>
                            <setting name="RecentFilesCount" serializeAs="String">
                                <value>30</value>
                            </setting>
                            <setting name="CheckExt" serializeAs="String">
                                <value>.avi,.wmv,.mpg,.flv,.asf,.mpeg,.mkv,.swf,.ogm,.mp4,.mov,.mod,.avs,.divx,.3gp,.3g2,.m4v,.zip</value>
                            </setting>
                            <setting name="DefaultZipViewerPath" serializeAs="String">
                                <value>C:\Users\dhama\AppData\Local\Programs\NeeLaboratory\NeeView\NeeView.exe</value>
                            </setting>
                            <setting name="LastWpfSkinName" serializeAs="String">
                                <value>DefaultSmall</value>
                            </setting>
                            <setting name="LastSkinEngine" serializeAs="String">
                                <value>WPF</value>
                            </setting>
                            <setting name="ThumbnailParallelism" serializeAs="String">
                                <value>8</value>
                            </setting>
                            <setting name="padding" serializeAs="String">
                                <value>XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX</value>
                            </setting>
                        </IndigoMovieManager.Properties.Settings>
                    </userSettings>
                </configuration>
                """;
            File.WriteAllText(good75, rich);
            Assert.True(new FileInfo(good75).Length >= 1500);

            string best = SettingsUpgrader.FindBestPreviousUserConfig(profile, "1.0.0.78");
            Assert.Equal(good75, best);
        }
        finally
        {
            Directory.Delete(profile, recursive: true);
        }
    }

    private static string CreateTempProfile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "imm-settings-upgrader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
