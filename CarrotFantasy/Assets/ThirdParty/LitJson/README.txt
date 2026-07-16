LitJson (JSON library)
======================

This folder is ONLY the LitJson JSON parser used by gameplay / editor tools
(MapConfig, LevelWave, BattleParam, etc.).

It is NOT the ILRuntime hot-update runtime.
Code hot-update in this project uses HybridCLR.

History: files used to live under Assets/ThirdParty/ILRuntime/LitJson/
(from an older ILRuntime-era fork of LitJson). They were moved here to avoid
confusion. Namespace remains LitJson; call sites do not need changes.
