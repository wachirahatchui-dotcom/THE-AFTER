# Rigging

โมเดลจาก Tripo มาแบบ **ไม่มีกระดูกเลย** และ Unity สร้างกระดูกให้ไม่ได้
(Humanoid ทำได้แค่ *แมป* กระดูกที่มีอยู่แล้ว) สคริปต์ในโฟลเดอร์นี้จึงสร้าง
armature ให้ก่อนด้วย Blender

> ใช้ **Blender 5.1** เพราะ addon MCP ติดตั้งอยู่ที่เวอร์ชันนั้น
> (5.2 ก็รัน headless ได้ แต่สั่งจากแชตไม่ได้)

## ไฟล์

| ไฟล์ | ทำอะไร |
|---|---|
| `probe_matha.py` | วัดสัดส่วน mesh (ความสูง, แกน up, ความกว้างแต่ละระดับ) |
| `rig_matha.py` | สร้าง rig แล้ว **export FBX ทับ** `Assets/Models/Characters/Matha/Matha.fbx` |
| `build_matha_blend.py` | เหมือนกันทุกอย่าง แต่ **save เป็น `Matha_Rig.blend`** ไว้เปิดดู/แก้มือ |
| `SourceMeshes/` | mesh ต้นฉบับ **ก่อน** rig — ต้องรันจากไฟล์นี้เสมอ |

## วิธีรัน

```bash
"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background --factory-startup --python rig_matha.py
```

สคริปต์หาตำแหน่งข้อต่อจากตัว mesh เอง (ไล่สแกนทีละชั้นความสูง ไม่ได้เดา) แล้ว
สร้าง 21 กระดูกชื่อแบบ Mixamo + skin ด้วย bone-heat weights

**ขั้นเก็บกวาดที่สำคัญ** (อยู่ในสคริปต์แล้ว) — centroid ของ mesh slice โกหก:
หน้าอกกับใบหน้าดึงจุดกลางลำตัวเบี้ยว และซ้าย/ขวาไล่แยกกันเลยไม่ตรงกัน
เห็นชัดตอนเปิดใน GUI (กระดูกสันหลังคดหน้า-หลัง 3.6 ซม. และข้อไหล่ขวาโผล่นอกผิว)
สคริปต์จึงจัดลำตัวให้ตรงแกน มิเรอร์ซ้าย→ขวา และหาโคนกระดูกไหปลาร้าที่อยู่ในตัวจริง

## หลัง export ต้องทำใน Unity

1. ถ้า **เปลี่ยนชื่อหรือย้ายตำแหน่งกระดูก** ให้สลับ importer เป็น `Generic` +
   `humanDescription = new HumanDescription()` ก่อน ไม่งั้นจะได้
   `Avatar creation failed: Transform 'X' not found in HumanDescription`
2. ตั้ง `Human` + `avatarSetup = CreateFromThisModel`
3. Unity จะ **ข้าม Chest** ต้องเติม `HumanBone{humanName="Chest", boneName="Chest"}` เอง
4. ตั้ง `scaleFactor` — Matha = **1.685124** ได้ความสูง 1.650 m พอดี

### กับดักการวัดความสูง

**ห้ามวัดจาก `SkinnedMeshRenderer.bounds`** — มันเป็นกล่องประมาณที่พองกว่าตัวจริงมาก
(อ่านได้ 1.65 ทั้งที่ mesh สูงจริง 1.35) ต้อง `BakeMesh()` แล้ววัดจาก vertex จริง

เตือนสีเหลือง `Avatar Rig Configuration mis-match` ระดับ 1–4 มม. ที่ขา
เป็นเรื่องปกติ ไม่ต้องแก้ — Unity เทียบ T-pose ที่มันบังคับกับไฟล์
