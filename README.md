# FBXNormalDisabler
FBXのBlendShape法線を無効化するツールです

## 概要
Blender5.1からシェイプキーの法線がFBXに出力できるようになりました🎉  
しかし、Unityのインポート設定でBlendShapeNormalをimportにすると、カスタム法線が崩れてしまいます💀  
このツールをつかって法線を維持したいブレンドシェイプにチェックを入れると、カスタム法線を無効化してくれます　

## どんなときにつかう
Breast_Big/Breast_Flat/HighHeelに連動して法線も変わって欲しい  
けど、BlendShapeNormalをimportにすると顔のカスタム法線が崩れちゃう!とき

## 注意
元のFBXを編集できるアバター作者向けのツールです  
普通にBreast_Flatの法線再計算したいだけなら[Pefabulous by hai-vr](https://docs.hai-vr.dev/docs/products/prefabulous)の`Recalculate Normals`とか使ってください

## 使い方
1. FBX ExporterをPackageManagerからインストール
2. FBXNormalDisablerWindow.csをコピって適当なフォルダに配置
3. Tools->PROTOCO->FBXNormalDisablerでツール起動
4. 法線を消したいブレンドシェイプにチェックして実行
