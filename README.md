# FileNametag

複数人でプロジェクトを運用するときに「このフォルダ・ファイルは誰が触るか」を明文化するための Unity エディタ拡張です。
Project ウィンドウのフォルダ・ファイルアイコンの右下に、担当者の名前タグを表示します。

## 特徴

- **Project Settings で名前を管理**: 名前タグに使う名前を Project Settings > NameTag Settings で登録できます。
- **インスペクタから割り当て**: フォルダやファイルを選択し、インスペクタ下部（Asset Labels の上）のプルダウンから名前タグを設定できます。複数選択での一括設定にも対応しています。
- **アイコン右下にタグを表示**: 名前タグは白い角丸背景付きで表示されるので、どのアイコンの上でも読みやすくなっています。
- **親フォルダから継承**: ファイルに名前タグがない場合は、親フォルダの名前タグを表示します。ファイル自身に名前タグを設定すると、そちらが優先されます。
- **チームで共有**: 設定は `ProjectSettings/NameTagSettings.asset` に保存されるため、バージョン管理を通じてチーム全員で共有できます。

## 動作環境

- Unity 6000.3（動作確認済み）

## インストール

Unity の **Window > Package Manager** を開き、左上の **+ > Install package from git URL...** に次の URL を入力します。

```
https://github.com/HyogaFukuno/FileNametag.git?path=Packages/com.hyogafukuno.nametag#v1.0.1
```

または `Packages/manifest.json` の `dependencies` に直接追加します。

```json
{
  "dependencies": {
    "com.hyogafukuno.nametag": "https://github.com/HyogaFukuno/FileNametag.git?path=Packages/com.hyogafukuno.nametag#v1.0.1"
  }
}
```

末尾の `#v1.0.1` を外すと、`main` ブランチの最新版がインストールされます。

## 使い方

1. **Edit > Project Settings > NameTag Settings** を開き、**Names** に名前タグとして使う名前を登録します。
2. Project ウィンドウでフォルダまたはファイルを選択します。
3. インスペクタ下部の **Name Tag > Assignee** から名前を選びます。
4. Project ウィンドウのアイコン右下に名前タグが表示されます。

名前タグを外すときは、**Assignee** で **(なし)** を選びます。

## 表示ルール

| 対象 | 表示される名前タグ |
| --- | --- |
| フォルダ | 自身に設定された名前タグ |
| ファイル（名前タグ未設定） | 最も近い親フォルダの名前タグ（グレーの斜体で表示） |
| ファイル（名前タグ設定済み） | 自身に設定された名前タグ（親フォルダより優先） |

- Project ウィンドウを 1 行のリスト表示にしている場合は、行の右端に表示します。
- NameTag Settings から削除した名前のタグは表示されなくなります（割り当て自体は残り、インスペクタでは「(未登録)」と表示されます）。

## データの保存先

登録した名前と割り当ては `ProjectSettings/NameTagSettings.asset` に保存されます。
割り当てはアセットの GUID で管理しているため、ファイルを移動・リネームしても名前タグは外れません。
削除したアセットの割り当ては、NameTag Settings の「存在しないアセットの割り当てを削除」で整理できます。

## リポジトリ構成

```
FileNametag/
├── Packages/
│   └── com.hyogafukuno.nametag/   # パッケージ本体（git URL で配布）
│       ├── Editor/                # エディタ拡張のソースコード
│       ├── package.json
│       ├── README.md
│       ├── CHANGELOG.md
│       └── LICENSE.md
├── Assets/                        # 開発・動作確認用の Unity プロジェクト
└── ProjectSettings/
```

このリポジトリは開発用の Unity プロジェクトで、パッケージは `Packages/com.hyogafukuno.nametag` に embedded package として置いています。

## ライセンス

[MIT License](LICENSE)
