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

## ปิด/เปิด autosync ชั่วคราว

```powershell
.\Tools\GitAutosync\stop-autosync.ps1
.\Tools\GitAutosync\start-autosync.ps1
```

ดู log กิจกรรมที่ `Tools\GitAutosync\autosync.log`
