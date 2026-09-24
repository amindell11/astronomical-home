using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Substrate.Services.Environment.Flat
{
    [ScriptedImporter(2, "flatbg")]
    public sealed class FlatBackgroundImport : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string manifestJson;
            JObject manifest;
            byte[] pixels;
            int width;
            int height;
            Color background;
            Color primary;
            Color secondary;
            Color accent;
            bool final;
            try
            {
                using var archive = ZipFile.OpenRead(ctx.assetPath);
                manifestJson = new UTF8Encoding(false, true).GetString(ReadEntry(archive, "manifest.json", 1024 * 1024));
                manifest = JObject.Parse(manifestJson, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (Integer(manifest, "schema_version") != 1)
                    throw new InvalidDataException("schema_version must be 1.");
                width = Integer(manifest, "width");
                height = Integer(manifest, "height");
                if (width < 2 || width > 8192 || height < 2 || height > 8192)
                    throw new InvalidDataException("width and height must each be between 2 and 8192.");
                string stage = Text(manifest, "stage");
                if (stage != "draft" && stage != "final")
                    throw new InvalidDataException("stage must be draft or final.");
                final = stage == "final";
                if (!(manifest["provenance"] is JObject))
                    throw new InvalidDataException("provenance must be an object.");
                if (!(manifest["palette"] is JObject palette))
                    throw new InvalidDataException("palette must be an object.");
                background = PaletteColor(palette, "base");
                primary = PaletteColor(palette, "primary");
                secondary = PaletteColor(palette, "secondary");
                accent = PaletteColor(palette, "accent");
                pixels = ReadEntry(archive, "image.rgba16f", width * height * 8);
                if (pixels.Length != width * height * 8)
                    throw new InvalidDataException("image.rgba16f length does not match width and height.");
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(pixels)).Replace("-", "").ToLowerInvariant();
                if (Text(manifest, "pixel_sha256") != hash)
                    throw new InvalidDataException("image.rgba16f SHA256 does not match pixel_sha256.");
                for (int i = 0; i < pixels.Length; i += 2)
                    if ((pixels[i + 1] & 0x7c) == 0x7c)
                        throw new InvalidDataException("image.rgba16f contains a non-finite half value.");
                var exr = Entry(archive, "image.exr");
                using var exrStream = exr.Open();
                if (exrStream.ReadByte() != 0x76 || exrStream.ReadByte() != 0x2f ||
                    exrStream.ReadByte() != 0x31 || exrStream.ReadByte() != 0x01)
                    throw new InvalidDataException("image.exr is missing its OpenEXR signature.");
            }
            catch (Exception error) when (error is InvalidDataException || error is JsonException ||
                                          error is IOException || error is DecoderFallbackException ||
                                          error is OverflowException)
            {
                ctx.LogImportError($"Invalid flat background bundle: {error.Message}");
                return;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBAHalf, true, true)
            {
                name = Path.GetFileNameWithoutExtension(ctx.assetPath) + " Texture",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            if (!BitConverter.IsLittleEndian)
                for (int i = 0; i < pixels.Length; i += 2)
                    (pixels[i], pixels[i + 1]) = (pixels[i + 1], pixels[i]);
            texture.SetPixelData(pixels, 0);
            texture.Apply(true, true);
            var asset = ScriptableObject.CreateInstance<FlatBackgroundAsset>();
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            asset.Initialize(texture, final, background, primary, secondary, accent, manifestJson);
            ctx.AddObjectToAsset("texture", texture);
            ctx.AddObjectToAsset("background", asset);
            ctx.SetMainObject(asset);
        }

        private static ZipArchiveEntry Entry(ZipArchive archive, string name)
        {
            ZipArchiveEntry found = null;
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName != name)
                    continue;
                if (found != null)
                    throw new InvalidDataException($"Duplicate {name} entry.");
                found = entry;
            }
            return found ?? throw new InvalidDataException($"Missing {name} entry.");
        }

        private static byte[] ReadEntry(ZipArchive archive, string name, int maximumLength)
        {
            var entry = Entry(archive, name);
            if (entry.Length > maximumLength)
                throw new InvalidDataException($"{name} exceeds its allowed length.");
            using var input = entry.Open();
            var bytes = new byte[(int)entry.Length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = input.Read(bytes, offset, bytes.Length - offset);
                if (read == 0)
                    throw new InvalidDataException($"{name} is truncated.");
                offset += read;
            }
            if (input.ReadByte() != -1)
                throw new InvalidDataException($"{name} exceeds its declared length.");
            return bytes;
        }

        private static int Integer(JObject obj, string name)
        {
            if (obj[name]?.Type != JTokenType.Integer)
                throw new InvalidDataException($"{name} must be an integer.");
            return obj[name].Value<int>();
        }

        private static string Text(JObject obj, string name)
        {
            if (obj[name]?.Type != JTokenType.String)
                throw new InvalidDataException($"{name} must be a string.");
            return obj[name].Value<string>();
        }

        private static Color PaletteColor(JObject palette, string name)
        {
            if (!(palette[name] is JArray values) || values.Count != 3)
                throw new InvalidDataException($"palette.{name} must contain three linear RGB values.");
            var color = new Color(0, 0, 0, 1);
            for (int i = 0; i < 3; i++)
            {
                if (values[i].Type != JTokenType.Float && values[i].Type != JTokenType.Integer)
                    throw new InvalidDataException($"palette.{name} must be numeric.");
                float value = values[i].Value<float>();
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                    throw new InvalidDataException($"palette.{name} must be finite and nonnegative.");
                color[i] = value;
            }
            return color;
        }
    }
}

