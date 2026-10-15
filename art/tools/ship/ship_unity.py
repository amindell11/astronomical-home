"""Write the Unity side of a painted ship: mask textures with import settings and one
_PaintLayers material per palette per region, as files Unity reads unchanged.

Pure Python. The bundle lands under <out>/unity/<Name>/ (Paint/, Materials/<palette>/) with
.meta files whose GUIDs are derived from the ship name and path, so a re-run keeps every
reference; a visuals folder passed as `existing` lends the GUIDs of files already there.
"""

from pathlib import Path
import re
import shutil
import uuid

import ship_contract as contract

SHADER_META = Path(__file__).resolve().parents[3] / "src/Asteroids3D/Assets/Visuals/Shaders/DrawnSurface.shader.meta"
GUID_LINE = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.MULTILINE)
MATERIAL_FLOATS = {
    "_AmbientStrength": 0.35, "_BumpScale": 1, "_CastShadowStrength": 0.6, "_DebrisVisibility": 1,
    "_DetailAlbedoMapScale": 0, "_EmissionStrength": 0.35, "_LineSoftness": 0.08, "_LineStrength": 0,
    "_LineThreshold": 0.12, "_PaintEmissionBlend": 0, "_PaintLayers": 1, "_PaintSaturation": 1,
    "_PaletteLighting": 1, "_PigmentPreservation": 0, "_ShadowSoftness": 0.1, "_ShadowThreshold": 0.35,
    "_SootStrength": 0, "_SpecularStrength": 0, "_TextureStrength": 0, "_UseRelief": 0, "_WearStrength": 0,
}
MATERIAL_COLORS = {"_ShadowColor": [0.5, 0.6, 0.72]}
UNBOUND_MAPS = ("_BaseMap", "_BumpMap", "_DetailAlbedoMap", "_DetailMask", "_WearMap")
FOLDER_META = "fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
MATERIAL_META = "fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
TEXTURE_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {size}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: {size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def shader_guid():
    match = GUID_LINE.search(SHADER_META.read_text(encoding="utf-8"))
    if match is None:
        raise RuntimeError(f"No guid in {SHADER_META}")
    return match.group(1)


def srgb(linear):
    return [12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055 for v in linear[:3]]


def lift(colour, amount):
    return [v + (1 - v) * amount for v in colour[:3]]


def _f(value):
    return f"{float(value):.8g}"


def _rgba(colour):
    r, g, b = (_f(v) for v in colour[:3])
    return f"{{r: {r}, g: {g}, b: {b}, a: 1}}"


def _texture(name, guid):
    reference = f"{{fileID: 2800000, guid: {guid}, type: 3}}" if guid else "{fileID: 0}"
    return f"    - {name}:\n        m_Texture: {reference}\n        m_Scale: {{x: 1, y: 1}}\n        m_Offset: {{x: 0, y: 0}}\n"


def material_yaml(name, shader, maps, floats, colours):
    """Serialize one drawn-surface material in the shape Unity writes (Valis's Ivory.mat)."""
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!21 &2100000", "Material:",
             "  serializedVersion: 8", "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
             "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", f"  m_Name: {name}",
             f"  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}", "  m_Parent: {fileID: 0}",
             "  m_ModifiedSerializedProperties: 0", "  m_ValidKeywords:", "  - _PAINT_LAYERS",
             "  m_InvalidKeywords: []", "  m_LightmapFlags: 4", "  m_EnableInstancingVariants: 0",
             "  m_DoubleSidedGI: 0", "  m_CustomRenderQueue: -1", "  stringTagMap: {}",
             "  disabledShaderPasses: []", "  m_LockedProperties: ", "  m_SavedProperties:",
             "    serializedVersion: 3", "    m_TexEnvs:"]
    text = "\n".join(lines) + "\n"
    text += "".join(_texture(key, maps[key]) for key in sorted(maps))
    text += "    m_Ints: []\n    m_Floats:\n"
    text += "".join(f"    - {key}: {_f(value)}\n" for key, value in sorted(floats.items()))
    text += "    m_Colors:\n"
    text += "".join(f"    - {key}: {_rgba(value)}\n" for key, value in sorted(colours.items()))
    return text + "  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n"


class Bundle:
    """The files under <out>/unity/<Name>/, each with a GUID that survives re-runs."""

    def __init__(self, out, name, existing=None):
        self.root = Path(out) / "unity" / name
        self.name = name
        self.existing = Path(existing).resolve() if existing else None
        self.files = []

    def guid(self, relative):
        if self.existing is not None:
            meta = self.existing / f"{relative}.meta"
            if meta.is_file():
                match = GUID_LINE.search(meta.read_text(encoding="utf-8"))
                if match:
                    return match.group(1)
        return uuid.uuid5(uuid.NAMESPACE_URL, f"astronomical-home/ship-paint/{self.name}/{relative}").hex

    def _write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")
        self.files.append(relative)

    def folder(self, relative):
        (self.root / relative).mkdir(parents=True, exist_ok=True)
        self._write(f"{relative}.meta", FOLDER_META.format(guid=self.guid(relative)))

    def texture(self, relative, source, size):
        (self.root / relative).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, self.root / relative)
        self.files.append(relative)
        guid = self.guid(relative)
        self._write(f"{relative}.meta", TEXTURE_META.format(guid=guid, size=size))
        return guid

    def material(self, relative, text):
        self._write(relative, text)
        self._write(f"{relative}.meta", MATERIAL_META.format(guid=self.guid(relative)))


def write_bundle(out, ship, masks, palettes, paint, emission_colours, existing=None):
    """Write the bundle; masks maps mask name to its PNG (Emission included when present).

    emission_colours maps a region to the linear colour of its painted indicators.
    Returns (bundle root, written relative paths).
    """
    bundle = Bundle(out, ship["name"], existing)
    shader = shader_guid()
    bundle.folder("Paint")
    guids = {mask: bundle.texture(f"Paint/{mask}.png", path, ship["texture_size"]) for mask, path in masks.items()}
    material = paint["material"]
    lighting = material["lighting"]
    maps = {key: None for key in UNBOUND_MAPS}
    maps.update({f"_Paint{mask}Map": guids[mask] for mask in contract.MASKS})
    maps["_EmissionMap"] = guids.get(contract.EMISSION)
    floats = dict(MATERIAL_FLOATS)
    floats.update({"_PaintShadowStrength": material["shadow_strength"],
                   "_PaintLightStrength": material["light_strength"],
                   "_PaintInkStrength": material["ink_strength"],
                   "_PaintEmissionBlend": material["emission_blend"] if contract.EMISSION in guids else 0})
    floats.update({key: lighting[key] for key in contract.LIGHTING_FLOATS if key in lighting})
    bundle.folder("Materials")
    for palette, colours in palettes["palettes"].items():
        bundle.folder(f"Materials/{palette}")
        for region in sorted(colours):
            base = colours[region]
            colour_values = {"_BaseColor": [1, 1, 1], "_OrangeGain": [1, 1, 1],
                             "_PaperColor": srgb(base),
                             "_PaintLightColor": srgb(lift(base, material["light_lift"])),
                             "_PaintInkColor": srgb(material["ink_color"]),
                             "_EmissionColor": emission_colours.get(region, [0, 0, 0]),
                             **{key: lighting.get(key, MATERIAL_COLORS[key]) for key in contract.LIGHTING_COLORS}}
            bundle.material(f"Materials/{palette}/{region}.mat", material_yaml(region, shader, maps, floats, colour_values))
    current = set(bundle.files)
    for path in sorted(bundle.root.rglob("*"), key=lambda p: len(p.parts), reverse=True):
        if path.is_file() and path.relative_to(bundle.root).as_posix() not in current:
            path.unlink()
        elif path.is_dir() and not any(path.iterdir()):
            path.rmdir()
    return bundle.root, bundle.files
