# ย้อนเวอร์ชั่น (Rollback)

Autosync commit ทุก ๆ ครั้งที่มีไฟล์เปลี่ยน (เช็คทุก 90 วินาที) ข้อความ commit
จะเป็น `Auto-sync 2026-09-03 22:15:00 (12 file(s))` เรียงตามเวลา

## ดูประวัติ

```bash
git log --oneline
```

## ดูว่า commit หนึ่ง ๆ เปลี่ยนอะไรไปบ้าง

```bash
git show <commit-hash>
```

## เอาไฟล์เดียวกลับไปเป็นเวอร์ชั่นเก่า (ไม่กระทบไฟล์อื่น)

```bash
git checkout <commit-hash> -- "Assets/Scripts/ตัวอย่าง.cs"
```

## ย้อนทั้งโปรเจกต์กลับไปที่ commit หนึ่ง แบบเก็บของใหม่ไว้เป็น commit ใหม่ (ปลอดภัยที่สุด)

```bash
git revert --no-commit <commit-hash>..HEAD
git commit -m "Revert to <commit-hash>"
```

## ย้อนทั้งโปรเจกต์กลับไปแบบทิ้งของหลังจากนั้นทั้งหมด (ระวัง - ลบถาวรถ้ายังไม่ push ที่อื่น)

```bash
git reset --hard <commit-hash>
git push --force
```

ใช้ตัวสุดท้ายนี้เฉพาะตอนแน่ใจจริง ๆ ว่าไม่ต้องการของหลังจากนั้นอีกเลย -
ตัวก่อนหน้า (`revert`) ปลอดภัยกว่าเพราะไม่ลบประวัติอะไรทิ้ง

## เวอร์ชั่น Backup หลัก (tag)

จุดเช็คพอยต์ใหญ่ที่ตั้งใจกันไว้ไม่ให้หาย ไม่ขยับตาม autosync รอบถัดๆ ไป
ดูทั้งหมดได้ที่ https://github.com/wachirahatchui-dotcom/THE-AFTER/releases

- `v0.1-main` - "THE AFTER V.01 MAIN"

**กู้กลับมาทั้งโปรเจกต์จาก tag (ทดสอบแล้วว่าใช้ได้จริง 100%):**

```bash
git clone --branch v0.1-main https://github.com/wachirahatchui-dotcom/THE-AFTER.git
```

**อย่าใช้ปุ่ม "Download ZIP" บนหน้าเว็บ Release/tag แทนคำสั่งข้างบน** - GitHub
ไม่แพ็กไฟล์ Git LFS (รูป/เสียง/โมเดล) ลงในซิปให้อัตโนมัติ ได้แต่ไฟล์ pointer
เล็ก ๆ แทนไฟล์จริง ต้อง `git clone` เท่านั้นถึงจะได้ไฟล์ใหญ่มาด้วยครบ

จะตั้ง tag ใหม่ตอนมีเวอร์ชั่นหลักถัดไป (เช่น V.02):

```bash
git tag -a "v0.2-main" -m "THE AFTER V.02 MAIN"
git push origin "v0.2-main"
```

## ปิด/เปิด autosync ชั่วคราว

```powershell
.\Tools\GitAutosync\stop-autosync.ps1
.\Tools\GitAutosync\start-autosync.ps1
```

ดู log กิจกรรมที่ `Tools\GitAutosync\autosync.log`
