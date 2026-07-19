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

These four technologies form the foundation shared by every TeamGadget synchronization project, including CEU, CEB, and future tools.

# English <br>
# Cascadeur Entangle for Unity (CEU) Beta Test<br>


# 日本語 <br>
# Cascadeur Entangle for GODOT (CEG) βテスト<br>
CEGはGODOT上のキャラクターとCascadeur上のキャラクターをリアルタイム同期する事を手段とし<br>
クリエイターのキャラクターアニメーション制作過程を支援する目的で作られたツールです。<br>

# 開発・動作環境<br>
Windows専用 (コード内でWindowsAPIを使用)<br>
Godot Engine v4.7.1.stable.mono.official(.NET)<br>
CascadeurPro 2026.1.3<br>

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
step4: インスペクターの`Target Skeleton`にそのキャラクターのボーン`Skeleton3D`をセットしてください<br>
step5: GODOT側`Scene`内の同期したいキャラクターに`CEG_Avatar_v1.cs`をアタッチ<br>
step6: GODOT UI 右上部の金槌アイコンを押すか`Alt+B`でコンパイルしてください<br>
step7: `CEG_System_v1.cs`をアタッチした`NODE(白丸)`のインスペクターで`Connect To Cascadeur`トグルをオン<br>
これで同期開始します。<br>

# トラブルシューティング <br>
1. キャラクターの`Target Port`番号は合ってますか？<br>
2. 8981ポートのキャラクターはプリフィックスは付きませんので`Cascadeur Prefix`は何も記入しません<br>
3. 8982ポート以降から`Cascadeur Prefix`に`character1:``character2:`とプリフィックスを記入します<br>
4. 同期順はCascadeurが先でGODOT側が後です<br>
5. GODOTのタイムラインを動かすとキャラクターがストロボ現象を起こします -> これは異常ではありません。<br>
　GODOTに限らずほぼ全てのソフトで起こる現象でタイムラインが持つアセットへの影響力が上位にある事で起こります。<br>
 最終的にオフラインベイクを実施してリアルタイム同期を切ればこの問題は解消されます。<br>
 これはシステム側の仕様ですので、今現在ではリアルタイム同期と綺麗に共存することはできません。<br>
6. GODOTのキャラクターとCascadeurのキャラクターのボーン階層構造と名称は一致させてください。<br>
7. 通信を快適する関係上、1体のキャラクターが持つボーンの数は0～254(255本)までとしています。<br>
 それ以上のボーンを持つキャラクターも同期できますが、超えた分は無視されます。<br>

# Cascadeurでの複数キャラクターセットアップ方法<br>
例 : 2体セットアップ<br>
1. シーンを作成、最初の1体目を通常通りインポート -> リギング。<br>
2. 2体目用に更にシーンを作ります。そのまま2体目を通常通りインポート -> リギング。<br>
3. 2体目が居るシーンを保存して閉じます。<br>
4. 1体目が居るシーンに戻って、`File -> Import -> Import Scene To Current...`で2体目のシーンをインポート。<br>
5. 2体目のボーン名に自動でcharacter1:のプレフィックスが付与されます。<br>
6. 3体目も同じ手順となります。<br>

# オフライン・ベイク<br>
1. <br>
2. <br>
3. <br>
4. <br>

Team Gadget Youtube<br>
https://www.youtube.com/channel/UCj9OYwzMAIgYAeVkTV4wczw<br>

# 免責事項 <br>
CEUはTeamGadgetによる独立したプロジェクトです。<br>
CascadeurはNekkiの商標または財産です。 <br> 
Unityはunity technologies incの商標または財産です。<br>

本プロジェクトは、Nekkiまたはunity technologies incによる公式製品ではなく、承認、提携、スポンサー提供、または公式サポートを受けたものではありません。<br>
