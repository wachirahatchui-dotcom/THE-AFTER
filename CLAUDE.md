# THE AFTER — คู่มือสำหรับ Claude

เกมเล่าเรื่องมุมมองบุคคลที่หนึ่ง Unity **6000.5.0f1** URP

**คุยกับผู้ใช้เป็นภาษาไทย** และ**ถามให้ชัดก่อนเริ่มทำ** ทุกครั้ง โดยเฉพาะงานที่ผู้ใช้ไม่ได้ระบุรายละเอียด
เช่น มุมกล้อง การเคลื่อนกล้อง หรือ Animation สำคัญ ๆ

---

## กฎที่ห้ามพลาด

**ห้าม `EditorSceneManager.OpenScene` โดยไม่เช็ค `isDirty` ก่อน** เคยทำงานจัดตำแหน่งของผู้ใช้หายถาวรมาแล้ว
เซฟซีนที่ค้างอยู่ก่อนเสมอ แล้วค่อยแตะอะไร

**ห้ามเรียก `director.Evaluate()` หรือ scrub Timeline ในโหมดแก้ไข** มันเลื่อนตำแหน่งของจริงในซีน
เคยทำถ้วยกับตัวละครขยับไปแล้วสองครั้ง ถ้าต้องพรีวิวจริง ๆ ให้เก็บ transform เดิมไว้แล้วคืนค่าให้ครบ

**ห้าม `git reset --hard`** ผู้ใช้มักมีงานจัดฉากที่ยังไม่ commit อยู่เสมอ

**อ้างว่าอะไรทำงานได้ ต้องวัดก่อน** `EditorApplication.Step()` เดินเฟรมได้แม้ Unity ไม่ได้โฟกัสหน้าต่าง
ใช้ทดสอบ runtime ได้เต็มที่ ไม่มีข้ออ้างว่าตรวจไม่ได้ (`PlayerSettings.runInBackground` มีผลกับ build เท่านั้น)

---

## ซีน

| ซีน | คืออะไร |
|---|---|
| `Assets/Scenes/MainMenu.unity` | เมนูหลัก ซีนแรกใน Build Settings |
| `Assets/Scenes/Chapter1.unity` | **ซีนที่เกมใช้จริง** บทที่ 1 ทั้งบท |
| `Assets/Scenes/Sandbox.unity` | ที่ทดลอง รวมทุกอย่างไว้ — **ผู้ใช้สั่งให้คงไว้เหมือนเดิม อย่าแก้** |

Chapter1 แบ่งของเป็นชุดตาม stage: `=== Stage 1 Set (ห้องนอน) ===` / `=== Stage 2 Set (บังเกอร์) ===` /
`=== Stage 3 Set (โรงรถ) ===` ทุกชุดขึ้นทะเบียนใน `CutsceneChapter1.stageSets` เปิดทีละชุดด้วย `ShowOnly(i)`
ชุดที่ยังไม่ถึงถูกปิดไว้ทั้งหมด เพราะของที่เปิดทิ้งไว้ยังกินหน่วยความจำและทอดเงาลงช็อตที่มันไม่ได้อยู่

Stage 3 ไม่มีใครเรียก `ShowOnly(2)` — ประตูดำเป็นคนเปิดชุดเองผ่าน `TeleportGate.activateOnUse`
เพราะเกตขากลับอยู่ในโรงรถ ถ้าชุดดับมันก็ดับไปด้วย เปิดตัวเองไม่ได้

---

## แก้ที่เดียวจบ

ระบบพวกนี้ออกแบบให้เพิ่มของใหม่โดยแตะไฟล์เดียว **อย่าไปสร้าง path ที่สองขนานกัน**

| จะเพิ่ม | แก้ที่ |
|---|---|
| สี ฟอนต์ ขนาด ของ UI ทั้งเกม | `Assets/Resources/MenuTheme.asset` (schema อยู่ที่ `UI/Theme/MenuThemeAsset.cs`) |
| setting ใหม่ในหน้า Settings | `Scripts/Settings/SettingsCatalog.cs` |
| ไอเทมใหม่ใน Inventory | `Scripts/Inventory/ItemCatalog.cs` |
| stage ใหม่ใน Chapter 1 | เพิ่ม entry ใน `stageSets` + เรียก `ShowOnly()` |

**UI ทั้งหมดสร้างด้วยโค้ดล้วน ไม่มี prefab ไม่มี Canvas ที่ต้องลากใน Inspector**
ค่าที่ปรับได้อยู่ใน MenuTheme.asset ที่เดียว — แก้ค่าใน asset ไฟล์ตรง ๆ ถ้าตัวที่โหลดในหน่วยความจำไม่ยอมอัปเดต

---

## ตัวละครกับ Animation

ตัวละครทุกตัว (Asher / Logan / Ethan / Sydney / Baena / Alex) แปลงมาจาก Tripo
ริก **generic 41 กระดูก** ลำดับชั้นเหมือนกันหมด → **ไฟล์ `.anim` ใช้ข้ามตัวได้เลย**

- **ต้องเป็น Generic ห้ามเป็น Humanoid** ไม่งั้น Timeline Animation Track จะทับ transform curve ทิ้ง
- **หมุนกระดูกให้อ้างแกนจากโลก** `bone.parent.InverseTransformDirection(worldAxis)` ไม่ใช่แกน local
- **ความสูงตัวละครวัดด้วย `BakeMesh()`** `Renderer.bounds` พองกว่าจริงราว 20%
- `AnimationClip.SampleAnimation(go, t)` เล่นคลิปได้โดยไม่ต้องมี AnimatorController

รายละเอียดกับดักของคัตซีนอีกยาวอยู่ในหน่วยความจำ `the-after-cutscene-timeline`

---

## เขียนสคริปต์ผ่าน `Unity_RunCommand`

- คลาสต้องชื่อ `internal class CommandScript : IRunCommand` เท่านั้น
- **ใช้ `System.Reflection` ไม่ได้** ถูกบล็อก — ใช้ `SerializedObject` แทน
- `Mesh` เป็น namespace ในแอสเซมบลีนี้ ต้องเขียน `UnityEngine.Mesh` เต็ม
- `{0,+3}` ไม่ใช่ format specifier ที่ถูกต้องของ .NET

**ย้ายของข้ามซีน** ใช้ `Unsupported.CopyGameObjectsToPasteboard()` + paste แล้วค่อย `MoveGameObjectToScene`
paste จะตกในซีนของต้นฉบับเสมอ ไม่สนใจ `SetActiveScene` แต่ข้อดีคือ prefab link และอ้างอิงระหว่างของ
ในชุดเดียวกันไม่ขาด — ต้องเลือกทุกชิ้นแล้ว copy **ครั้งเดียว** ที่เหลือที่ยังชี้กลับซีนเดิม
ให้เดิน `SerializedObject` หาคู่ตาม path เต็มแล้วเซ็ตใหม่

---

## Git

`main` · remote `origin` · **ใช้ Git LFS** สำหรับ .fbx .obj .png .wav .mp3 .ttf .unitypackage (~800 MB)

- แท็กเวอร์ชันใหญ่: ref เป็นชื่อสั้น (`v0.2-stage3-bug`) ชื่อเต็มอยู่ในข้อความแท็ก
  เพราะ git ref มีเว้นวรรคไม่ได้
- `Build/` อยู่ใน .gitignore — ไฟล์ build ขึ้นทาง GitHub Releases ไม่ใช่ commit เข้ารีโป
- วิธีตั้งค่าเครื่องใหม่อยู่ใน [README.md](README.md)
