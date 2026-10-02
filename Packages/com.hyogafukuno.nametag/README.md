# NameTag

複数人でプロジェクトを運用するときに「このフォルダ・ファイルは誰が触るか」を明文化するための Unity エディタ拡張です。
Project ウィンドウのフォルダ・ファイルアイコンの右下に名前タグを表示します。

## インストール

Package Manager の **+ > Install package from git URL...** に次の URL を入力します。

```
https://github.com/HyogaFukuno/FileNametag.git?path=Packages/com.hyogafukuno.nametag
```

または `Packages/manifest.json` の `dependencies` に追加します。

```json
"com.hyogafukuno.nametag": "https://github.com/HyogaFukuno/FileNametag.git?path=Packages/com.hyogafukuno.nametag"
```

特定のバージョンに固定する場合は、URL の末尾にタグを指定します（例: `#v1.0.2`）。

## 使い方

1. **Project Settings > NameTag Settings** で名前タグに使う名前を登録します。
2. Project ウィンドウでフォルダまたはファイルを選択し、インスペクタ下部の **Name Tag > Assignee** から名前を選びます。
3. アイコンの右下に名前タグが表示されます。

## 表示ルール

- フォルダは、自身に設定された名前タグを表示します。
- ファイルは、自身に名前タグがなければ最も近い親フォルダの名前タグを表示します（グレーの斜体で表示）。
- ファイル自身に名前タグが設定されている場合は、そちらを優先して表示します。
- Project ウィンドウを 1 行のリスト表示にしている場合は、行の右端に表示します。

## データの保存先

登録した名前と割り当ては `ProjectSettings/NameTagSettings.asset` に保存されます。
このファイルをバージョン管理に含めることで、チーム内で名前タグを共有できます。
割り当てはアセットの GUID で管理しているため、ファイルを移動・リネームしてもタグは外れません。

## 動作確認環境

- Unity 6000.3

## ライセンス

[MIT License](LICENSE.md)
