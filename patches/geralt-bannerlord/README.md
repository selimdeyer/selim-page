# Geralt / MixMashMagicArmorPack — Compatibility Patch
**Unofficial patch v1.0.0 · Norwynx · 5 November 2026**

[**Download the patch ZIP**](https://raw.githubusercontent.com/selimdeyer/selim-page/main/patches/geralt-bannerlord/Geralt_Bannerlord_Compatibility_Patch.zip) · [SHA-256](Geralt_Bannerlord_Compatibility_Patch.zip.sha256.txt) · [Mod list video](https://youtu.be/rowdbbeV8KA)

## Türkçe kurulum

Bu küçük yama **orijinal modun yerine geçmez**. Kaynak modun aşağıdaki sürümü ve Bannerlord.Harmony gerekir.

1. Bannerlord'u kapatın. ZIP'i indirin ve **ayrı bir klasöre tamamen çıkartın**.
2. **Kurulum.cmd** dosyasına çift tıklayın.
3. Sorulduğunda `Modules\MixMashMagicArmorPack` klasörünüzün tam yolunu tırnaksız yapıştırın.
4. Betik 23 kaynak dosyanın SHA-256 özetlerini kontrol eder. Farklı/değiştirilmiş sürümde dosyaları değiştirmeden durur. Uyuşmazlığı zorlamayın.
5. `APPLIED` sonucunu ve yazdırılan `_GeraltCompatibilityBackup_...` klasörünü saklayın.
6. Başlatıcıda **Harmony, MixMashMagicArmorPack'ten önce** yüklensin.

Erişim engellenirse mod klasörüne yazma izni gerekir; gerekirse Kurulum.cmd'yi yönetici olarak çalıştırın. Windows güvenlik yazılımının verdiği bir uyarıyı aşmayın; kaynak kodu pakettedir ve dosya özeti aşağıdadır.

**Geri alma:** Oyunu kapatın, **Geri-Al.cmd** dosyasını açın; aynı mod klasörünü ve kurulumun yedek klasörünü belirtin. Özgün manifest geri gelir; yalnız yamanın eklediği DLL kaldırılır.

## Gereken kaynak / Required original

- [Original MixMashMagicArmorPack / Mixed weapon package – Magic version](https://www.nexusmods.com/mountandblade2bannerlord/mods/7544)
- Exact local source archive: `Mixed weapon package-Magic version-7544-V1-1-6-1733968879.rar`
- Original archive SHA-256: `2F58410AFCA1A4BDBE3B4BADF782B4BB0A88D055606F260E571773EE40A90F3C`
- Filename version: V1.1.6; internal manifest: v1.0.0. The supplied 23-file hash manifest is authoritative.
- Observed game: **Bannerlord v1.4.8 / runtime build 119303**. Observed Harmony module: **v2.4.2.248**.
- Current original-mod download availability and its exact association with Nexus file ID 44775 were not verified. You must obtain the original yourself. This repository does not mirror it.

## English installation

Close Bannerlord, fully extract the ZIP into a separate folder, run **Kurulum.cmd**, and enter the full path to your original mod folder. The script checks all 23 source files before writing, backs up SubModule.xml, registers Harmony/the compatibility shim, and adds only the new compatibility DLL. Load Harmony first. Run **Geri-Al.cmd** to restore. Detailed English instructions and the exact source manifest are inside the ZIP.

## What was verified

A complete temporary source extraction was patched; all 24 runtime files matched the working reference by SHA-256. Rollback restored all 23 original hashes. Wrong source/corrupt payload/backup/changed shim/repeated installation guards passed. Denied XML replacement rolled back the added shim. The ZIP was checked and the extracted release was tested again.

Recovered shim source rebuilt with equivalent IL/constant data; all 74 shim method/field references resolved. An isolated original OnMissionTick probe verified 62 instructions and exactly one targeted helper substitution.

**No new in-game visual test was performed.** All original ModuleData and asset packages remain unchanged, including Geralt face/hair/beard data, Kaer Morhen armor and Witcher sword data. The reference installation's working status was supplied by its user.

**Known limitations:** additional legacy API calls remain in optional trader and weapon flame-effect paths. This reproduces the recovered repair; it is not a claim of complete mod compatibility or compatibility with other game versions.

## Credits, rights and AI disclosure

Credits: the original MixMashMagicArmorPack creators and all respective asset rights holders; see the [original mod page](https://www.nexusmods.com/mountandblade2bannerlord/mods/7544). The Witcher 3 content remains with CD PROJEKT RED and its respective rights holders. This is unofficial and not endorsed by those creators.

Only the newly authored compatibility shim, scripts, documentation and hash lists are distributed here. **No original models, textures, TPACs, original mod DLLs, Harmony DLLs, PDBs or source archive are distributed.** No ownership transfer or relicensing of third-party content is claimed.

AI disclosure: compatibility code, installer scripts and documentation were created with OpenAI Codex. The source is included for inspection.

Patch ZIP: 20,609 bytes. SHA-256:
`0B45F317B1C4537E464E0CF830AE30D2F8C5269BDC44B6E73DEAEBF4BE5810EA`
