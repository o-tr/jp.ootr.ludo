# ootr式ルドー盤

VRChat ワールド向けのルドー盤ギミックです。

## 概要
- 2〜4人で遊べるルドー盤
- UdonSharp ベース
- Late Joiner を考慮した同期設計
- 盤面・駒・UI を分離した構成

## 対応環境
- Unity 2022.3
- VRChat Worlds SDK 3.6.1 以上
- UdonSharp 環境

## 依存パッケージ
- `com.vrchat.base`
- `com.vrchat.worlds`
- `jp.ootr.common`

## 導入
1. VPM でこのパッケージを導入します。
2. `Ludo.unity` または `Ludo.prefab` をワールドに配置します。
3. 必要に応じて UI、見た目、配置を調整します。

## 主な内容
- `Runtime/jp.ootr.ludo/Scripts/` : 実装スクリプト
- `Assets/Prefabs/` : 駒や演出用プレハブ
- `Assets/Materials/` : 盤面・駒用マテリアル
- `Assets/Textures/` : 盤面テクスチャ

## ルール概要
- 2〜4人でプレイ
- 各プレイヤーは駒を4つ持つ
- サイコロは1個
- 6 が出たときだけ待機中の駒を出場可能
- ぴったりの目でのみゴールへ進入可能
- 同じマスに重なった自色の駒はブロックとして扱う

詳細は `game_rules.md` を参照してください。

## ライセンス
- MIT
