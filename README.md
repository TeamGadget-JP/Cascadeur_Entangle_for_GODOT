# TeamGadget Core Architecture
**A universal real-time synchronization architecture for character animation across multiple DCC applications and game engines.**
The TeamGadget Core Architecture is a lightweight synchronization framework designed for real-time communication between independent animation applications.
Rather than relying on complex rig-specific solutions, TeamGadget focuses on four fundamental technologies that maximize performance, reliability, and interoperability while minimizing setup and runtime overhead.
## Core Technologies<br>
### Handshake Protocol<br>
Automatic initialization and synchronization of character information before streaming begins.<br>
### O(1) Runtime Cache<br>
Constant-time bone lookup for high-performance real-time synchronization.<br>
### Zero Calibration<br>
Automatic normalization of local transforms to establish a common reference space between different applications.<br>
### Smart Swizzle<br>
Automatic correction of coordinate-system differences using rest-pose quaternion analysis, enabling rig-agnostic synchronization without manual axis configuration.<br>

---

These four technologies form the foundation shared by every TeamGadget synchronization project, including CEU, CEB, CEG and future tools.

# English <br>
# Cascadeur Entangle for GODOT (CEG) Beta Test<br>
CEG is a tool designed to support creators in their character animation production workflow<br>
by enabling real-time synchronization between characters in Godot Engine and Cascadeur.<br>

# Development & Runtime Environment<br>
- **Windows Only** (uses Windows API internally)<br>
- **Tested Engine version:** Godot Engine v4.7.1.stable.mono.official (.NET)<br>
- **Tested Cascadeur version:** Cascadeur Pro 2026.1.3<br>
- It does not work with the free version of Cascadeur.<br>

# CEG Overview
1. Real-time synchronization.
2. Rig-Agnostic Synchronization.
3. Editor Mode and Run Project runtime synchronization.
4. Simultaneous multi-character synchronization.
5. Improved robustness by assigning a dedicated local port to each character.
6. Supports motions with root bone movement.
7. Built-in background offline baking.

# What is Rig-Agnostic Synchronization? (Verification Stage)
By dynamically handshaking the bone hierarchy and bone names from Godot to Cascadeur, 
the goal is to eliminate retargeting work as much as possible. In theory, 
any character that can be imported into Godot and Cascadeur and successfully 
rigged should be able to synchronize.

Whether it is a humanoid, quadruped, mechanical character, multi-legged creature, 
monster, or any other character that can be rigged in Cascadeur, 
it is expected that almost all of them can be synchronized.

# Local Port Assignment
- `8980` Reserved for the system
- `8981` Character 1
- `8982` Character 2
- `...`
- `8989` Reserved for offline baking

## Installation Steps
1. Place `CEG_Sender_v1.pyc` into the Cascadeur Python plugin folder:
   `[Cascadeur installation folder]\resources\scripts\python\commands\`
2. Import `CEG_System_v1.cs` and `CEG_Avatar_v1.cs` into your Godot project's `FileSystem`.

# Usage
- **Step 1:** Import the exact same character (identical skeleton structure and bone naming) into both Godot and Cascadeur.
  - **Step 1-1:** For humanoid rigs, it is an absolute requirement that the character is set to either an **A-pose** or **T-pose** in both applications.
  - **Step 1-2:** For non-humanoid rigs, ensure that the character is set to its **Rest Pose** in both applications.
- **Step 2:** In Cascadeur, select `Commands -> CEG_Sender_v1` to start the connection.
- **Step 3:** In your Godot `Scene` tree, create an empty `Node` (the standard white circle icon) and attach the `CEG_System_v1.cs` script to it.
- **Step 4:** Attach the `CEG_Avatar_v1.cs` script to the target character inside your Godot `Scene` tree.
- **Step 5:** In the Godot Inspector, set your character's `Skeleton3D` node into the `Target Skeleton` field.
- **Step 6:** Click the hammer icon in the top right of the Godot UI, or press `Alt + B` to build/compile the project.
- **Step 7:** Close the current scene once, then reload it.<br>
- **Step 7-1:** It is currently unclear whether this is due to Godot's behavior, but the port does not appear to open on the first attempt. Therefore, this step is required.<br>
- **Step 8:** In the Inspector of the `NODE (White Circle)` with `CEG_System_v1.cs` attached, enable the `Connect To Cascadeur` toggle.<br>
Sync is now complete!

# Troubleshooting
1. Double-check that your character’s `Target Port` number is assigned correctly.
2. For the character using port `8981`, no prefix is attached. Leave the `Cascadeur Prefix` field completely blank.
3. For characters assigned to port `8982` and onwards, you must manually enter the prefix (e.g., `character1:`, `character2:`, etc.) into the `Cascadeur Prefix` field.
4. Always establish the connection from **Cascadeur first**, then from the **Godot side**.
5. Moving the Godot timeline will cause the character to exhibit a "strobing" effect. **This is normal and expected behavior.** This occurs because the timeline's inherent priority and authority over assets override the real-time stream—a behavior common to almost all DCC tools and game engines. Once you run the offline bake and turn off real-time synchronization, this issue will resolve. This is a system-side limitation, so real-time streaming and timeline scrubbing cannot smoothly coexist at this time.
6. Ensure that the bone hierarchy structure and naming convention match exactly between your Godot character and Cascadeur character.
7. To optimize network communication and maintain high-performance streaming, the maximum number of synchronized bones per character is capped at **254** (bone IDs 0–253). Characters with more bones can still be synchronized, but any bones exceeding this limit will be ignored.
8. **If the character deforms/collapses upon starting synchronization:** This indicates that the Rig-Agnostic pipeline failed to resolve your rig. Since this version of CEG does not include manual alignment/offset features, your only option is to return to your character production/rigging workflow and re-verify your bone orientations and setups. If there is enough demand, we will look into adding manual adjustment features in future version updates.

# Setting Up Multiple Characters in Cascadeur
**Example: Setting up two characters**
1. Create a scene, then import and rig the first character as usual.
2. Create another separate scene for the second character. Import and rig the second character as usual.
3. Save and close the scene containing the second character.
4. Return to the first character's scene, then select `File -> Import -> Import Scene To Current...` and import the second scene.
5. The second character's bone names will automatically receive the `character1:` prefix.
6. Additional characters can be added sequentially using the same method.

# Offline Baking
1. First, create an `AnimationPlayer` node as a child of your character.
2. You can use the default `AnimationPlayer` generated during character import, but you must unlock it first.
3. In the character's Inspector, assign the target node to the `Target Anim Player` field.
4. In `Bake Animation Name`, type the exact name of the new animation you created in your `AnimationPlayer`.
5. Toggle `Enable Baking` to **ON**, and set the `Bake Interval` to `0.033` (this provides a standard 1:1 frame bake for 30fps). Setting it to `0.066` will bake at half-rate; adjust this according to your specific production needs.
6. In Cascadeur, disable timeline looping and play the animation. The baking process will begin.
7. To perform a retake, either select and delete all existing keyframes or configure a new animation name and bake again.

# Design Philosophy<br>
TeamGadget tools are designed to assist your workflow—not to become part of it.<br>
Once synchronization has served its purpose, simply bake the animation,<br> 
detach the synchronization components, and continue working with your project's native assets.<br>
The best synchronization tool is the one you no longer need.<br>

**TeamGadget YouTube Channel:**
https://www.youtube.com/channel/UCj9OYwzMAIgYAeVkTV4wczw

# Disclaimer
CEG is an independent project developed by TeamGadget.
Cascadeur is a trademark and/or property of Nekki.  
Godot Engine is a trademark and/or property of the Godot Foundation.

This project is not affiliated with, endorsed by, sponsored by, or officially supported by Nekki or the Godot Foundation.

---

# 日本語 <br>
# Cascadeur Entangle for GODOT (CEG) βテスト<br>
CEGはGODOT上のキャラクターとCascadeur上のキャラクターをリアルタイム同期する事を手段とし<br>
クリエイターのキャラクターアニメーション制作過程を支援する目的で作られたツールです。<br>

# 開発・動作環境<br>
Windows専用 (コード内でWindowsAPIを使用)<br>
Godot Engine v4.7.1.stable.mono.official(.NET)<br>
CascadeurPro 2026.1.3<br>
CascadeurFree版では動作しません。<br>

# CEGの概要<br>
1. リアルタイム同期<br>
2. リグ・アグノスティック同期<br>
3. エディター同期 / Run Project時同期<br>
4. 複数キャラクター同時同期<br>
5. キャラクター個別にローカルポートを割り当てることで堅牢化<br>
6. ルートボーン移動を伴うモーションに対応<br>
8. バックグラウンド・オフライン・ベイク搭載<br>

# リグ・アグノスティック同期とは?　（検証段階）<br>
GODOTから動的にボーン階層・名称をCascadeurとハンドシェイクすることにより<br>
リターゲット作業を徹底的に排除することを目標としました。GODOT、Cascadeurにインポートして<br>
リギングできるキャラクターなら理論上何でも同期できると考えています。<br>
それが例えば人間型であろうが四足型であろうがメカ、多足、クリーチャー、その他、Cascadeurで<br>
リギングできるキャラクターならほぼ全て同期できると予想しています。<br>

# ローカルポート割り当て <br>
8980 システム占有<br>
8981 キャラクター1<br>
8982 キャラクター2<br>
・<br>
・<br>
8989 オフラインベイク占有<br>

# 導入手順 <br>
1. `CEG_Sender_v1.pyc`をCascadeurのPythonプラグインフォルダに配置します。<br>
   `[Cascadeurインストール先]\resources\scripts\python\commands\`<br>
2. `CEG_System_v1.cs``CEG_Avatar_v1.cs`をGODOTの貴方のプロジェクトの`FileSystem`へインポート<br>

# 使用方法 <br>
step1: 同じ骨格構造・骨格命名を持つキャラクターを双方へインポート<br>
step1-1: 人型リグは双方とも必ずAポーズもしくはTポーズであることが絶対条件です<br>
step1-2: 非人型リグでは双方ともレスト・ポーズであることをご確認ください<br>
step2: Cascadeur側`Commands -> CEG_Sender_v1`を選択して通信開始。<br>
step3: GODOT側`Scene`に空の`NODE(白丸)`を作成して`CEG_System_v1.cs`をアタッチ<br>
step4: GODOT側`Scene`内の同期したいキャラクターに`CEG_Avatar_v1.cs`をアタッチ<br>
step5: インスペクターの`Target Skeleton`にそのキャラクターのボーン`Skeleton3D`をセットしてください<br>
step6: GODOT UI 右上部の金槌アイコンを押すか`Alt+B`でコンパイルしてください<br>
step7: 一旦シーンを閉じて、再度シーンを読み込んでください。
step7-1: GODOTの仕様なのか不明ですが初回だけポートを開いてくれないのでこの操作が必要となります。<br>
step8: `CEG_System_v1.cs`をアタッチした`NODE(白丸)`のインスペクターで`Connect To Cascadeur`トグルをオン<br>
これで同期開始します。<br>

# トラブルシューティング <br>
1. キャラクターの`Target Port`番号は合ってますか？<br>
2. 8981ポートのキャラクターはプリフィックスは付きませんので`Cascadeur Prefix`は何も記入しません<br>
3. 8982ポート以降から`Cascadeur Prefix`に`character1:``character2:`とプリフィックスを記入します<br>
4. 同期順はCascadeurが先でGODOT側が後です<br>
5. GODOTのタイムラインを動かすとキャラクターがストロボ現象を起こします -> これは異常ではありません。<br>
 GODOTに限らずほぼ全てのソフトで起こる現象でタイムラインが持つアセットへの影響力が上位にある事で<br>
 起こります。最終的にオフラインベイクを実施してリアルタイム同期を切ればこの問題は解消されます。<br>
 これはシステム側の仕様ですので、今現在ではリアルタイム同期と綺麗に共存することはできません。<br>
7. GODOTのキャラクターとCascadeurのキャラクターのボーン階層構造と名称は一致させてください。<br>
8. 通信を快適する関係上、1体のキャラクターが持つボーンの数は0～253(254本)までとしています。<br>
 それ以上のボーンを持つキャラクターも同期できますが、超えた分は無視されます。<br>
9. もし、同期を開始時にキャラクターが崩れたら... -> リグ・アグノスティック機能が通らなかった事を<br>
 意味します。CEGの今回のバージョンには手動調整機能は実装しませんでしたので、このケースが起こった<br>
 場合はキャラクターの制作過程に戻り、もう一度ボーンの向き等を見直して貰う他に手立てはありません。<br>
 今後、リクエストがあればバージョンアップを重ねる中で改良して行こうと考えております。<br>

# Cascadeurでの複数キャラクターセットアップ方法<br>
例 : 2体セットアップ<br>
1. シーンを作成、最初の1体目を通常通りインポート -> リギング。<br>
2. 2体目用に更にシーンを作ります。そのまま2体目を通常通りインポート -> リギング。<br>
3. 2体目が居るシーンを保存して閉じます。<br>
4. 1体目が居るシーンに戻って、`File -> Import -> Import Scene To Current...`で2体目のシーンをインポート。<br>
5. 2体目のボーン名に自動でcharacter1:のプレフィックスが付与されます。<br>
6. 3体目も同じ手順となります。<br>

# オフライン・ベイク<br>
1. まずキャラクターの子ノードとして`AnimationPlayer`を作ってください。<br>
2. キャラクターインポート時に付いてくる`AnimationPlayer`でも良いですがロックを外す必要があります。<br>
3. キャラクターのインスペクターで`Target Anim Player`をセットします。<br>
4. `Bake Animation Name`をセットした`AnimationPlayer`に新しく作ったアニメーション名と同じ名称を入力します。<br>
5. `Enable Baking`トグルをオンにして`Bake Interval`を`0.033`と設定します。これは1対1の設定です。<br>
　0.066にすればハーフになりますので、貴方の制作に合わせて調整してください。<br>
6. 後はCascadeur側のタイムラインのリピートを切って再生すればベイクが始まります。<br>
7. リテイクするにはキーフレームを全選択で削除するか、別テイクを設定してベイクしてください。<br>

Team Gadget Youtube<br>
https://www.youtube.com/channel/UCj9OYwzMAIgYAeVkTV4wczw<br>

# 免責事項 <br>
CEGはTeamGadgetによる独立したプロジェクトです。<br>
CascadeurはNekkiの商標または財産です。 <br> 
GODOTはGodot Foundationの商標または財産です。<br>

本プロジェクトは、NekkiまたはGodot Foundationによる公式製品ではなく、承認、提携、スポンサー提供、または公式サポートを受けたものではありません。<br>
