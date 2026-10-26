# スパイク結果(Task 1)

2026-10-27 にユーザーが実機で確認した結果。

| # | 確認 | 結果 | Task 7 への反映 |
|---|---|---|---|
| 1 | `/suzume info` | `CompanionData.CompanionId` は 0 のまま。ミニオン本体の `BaseId=247` で判別できる。平常時の DrawOffset は `<0,0,0>`。`Pos.Y` (37.09) が `MoveDest.Y` (36.09) より 1.0 高い | 検出は ChildObject + BaseId に変更済み。`BaseDrawOffset = 0` |
| 2 | `bounce` | 停止中も移動中も跳ねた | DrawOffset 方式で進める |
| 3 | `offsetx` | ずれがミニオンの回転に合わせて回った | `DrawOffsetIsLocal = true` |
| 4 | `spin` | 未報告 | 記録のみ |
| 5 | `pitch` | 前のめりになった | `PitchWrite = Update`、`PitchSign = 1` |
| 6 | `pitchdraw` | (pitch で成立したため不要) | |
| 7 | `anim <id>` | 未確認(ID を付けずに実行された) | 確認待ち |
| 8 | `freeze` | 未報告 | 記録のみ |
| 9 | `off` | 実行済み | |
| 追加 | 平常時の高さ | タビスズメは普段から宙に浮いている(論理位置の Y が地面より約 1y 高い) | 地面の高さの求め方を決める(確認待ち) |
