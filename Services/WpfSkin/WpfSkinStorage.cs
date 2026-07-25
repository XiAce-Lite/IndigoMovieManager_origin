using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>WPF スキンの保存・複製・削除。</summary>
    internal static class WpfSkinStorage
    {
        private const string SchemaRelativePath = "../skin.schema.json";

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly JsonSerializerOptions CloneOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        public static string GetSkinFolderPath(string skinName) =>
            Path.Combine(WpfSkinLoader.SkinsRoot, skinName);

        public static string GetSkinJsonPath(string skinName) =>
            Path.Combine(GetSkinFolderPath(skinName), WpfSkinLoader.DefinitionFileName);

        public static bool FolderExists(string skinName) =>
            !string.IsNullOrWhiteSpace(skinName) && Directory.Exists(GetSkinFolderPath(skinName));

        /// <summary>JSON 往復でディープコピーする。</summary>
        public static WpfSkinDefinition Clone(WpfSkinDefinition source)
        {
            if (source == null)
            {
                return WpfSkinLoader.CreateBuiltInDefault();
            }

            string json = JsonSerializer.Serialize(source, CloneOptions);
            WpfSkinDefinition clone = JsonSerializer.Deserialize<WpfSkinDefinition>(json, CloneOptions)
                ?? WpfSkinLoader.CreateBuiltInDefault();
            clone.Thumbnail ??= new WpfSkinThumbnail();
            clone.Card ??= new WpfSkinCard();
            clone.Card.Layout ??= new WpfSkinNode();
            clone.Surface ??= new WpfSkinSurface();
            clone.Styles ??= new Dictionary<string, WpfSkinStyle>();
            clone.FolderName = source.FolderName;
            return clone;
        }

        /// <summary>新規用: 既定テンプレートを複製して表示名だけ差し替える。</summary>
        public static WpfSkinDefinition CreateFromDefaultTemplate()
        {
            WpfSkinDefinition template = WpfSkinLoader.TryLoad(WpfSkinLoader.DefaultSkinName, out WpfSkinDefinition loaded)
                ? loaded
                : WpfSkinLoader.LoadDefault();
            WpfSkinDefinition clone = Clone(template);
            clone.Name = "新規スキン";
            clone.FolderName = null;
            return clone;
        }

        public static IReadOnlyList<string> EnumerateDeletableSkins() =>
            WpfSkinLoader.EnumerateSkins()
                .Where(name => !SkinNameSortHelper.IsProtectedDefaultSkin(name))
                .ToArray();

        /// <summary>
        /// フォルダ名として使えるか。Default 接頭辞や不正文字を拒否する。
        /// </summary>
        public static bool TryValidateFolderName(string folderName, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                errorMessage = "スキン名を入力してください。";
                return false;
            }

            string trimmed = folderName.Trim();
            if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                errorMessage = "スキン名に使えない文字が含まれています。";
                return false;
            }

            if (SkinNameSortHelper.IsProtectedDefaultSkin(trimmed))
            {
                errorMessage = "Default で始まる名前には保存できません。別名を指定してください。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// skin.json を書き出す。成功時 true。
        /// overwriteExisting=false かつ既存なら失敗。
        /// </summary>
        public static bool TrySave(
            WpfSkinDefinition definition,
            string folderName,
            bool overwriteExisting,
            out string errorMessage)
        {
            errorMessage = null;
            if (definition == null)
            {
                errorMessage = "スキン定義がありません。";
                return false;
            }

            if (!TryValidateFolderName(folderName, out errorMessage))
            {
                return false;
            }

            string trimmed = folderName.Trim();
            string folder = GetSkinFolderPath(trimmed);
            bool exists = Directory.Exists(folder) || File.Exists(GetSkinJsonPath(trimmed));
            if (exists && !overwriteExisting)
            {
                errorMessage = $"スキン「{trimmed}」は既に存在します。";
                return false;
            }

            if (SkinNameSortHelper.IsProtectedDefaultSkin(trimmed))
            {
                errorMessage = "Default 系スキンは上書きできません。";
                return false;
            }

            try
            {
                Directory.CreateDirectory(folder);
                // フォルダ名（コンボ・進捗）と表示名を揃える（名前を付けて保存で旧 name が残らないように）
                definition.Name = trimmed;
                string json = SerializeWithSchema(definition);
                File.WriteAllText(GetSkinJsonPath(trimmed), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TryDeleteToRecycleBin(string folderName, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                errorMessage = "スキン名が空です。";
                return false;
            }

            if (SkinNameSortHelper.IsProtectedDefaultSkin(folderName))
            {
                errorMessage = "Default 系スキンはアプリから削除できません。";
                return false;
            }

            string folder = GetSkinFolderPath(folderName.Trim());
            if (!Directory.Exists(folder))
            {
                errorMessage = "スキンフォルダが見つかりません。";
                return false;
            }

            return RecycleBinHelper.TrySendToRecycleBin(folder, out errorMessage);
        }

        private static string SerializeWithSchema(WpfSkinDefinition definition)
        {
            string body = JsonSerializer.Serialize(definition, WriteOptions);
            JsonNode node = JsonNode.Parse(body) ?? new JsonObject();
            var ordered = new JsonObject
            {
                ["$schema"] = SchemaRelativePath,
            };
            if (node is JsonObject obj)
            {
                foreach (KeyValuePair<string, JsonNode> pair in obj)
                {
                    if (string.Equals(pair.Key, "$schema", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ordered[pair.Key] = pair.Value?.DeepClone();
                }
            }

            return ordered.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + Environment.NewLine;
        }
    }
}
