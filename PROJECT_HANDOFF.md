# THE AFTER — Project Handoff

ชุดส่งต่อสำหรับเริ่ม **Claude Project ใหม่** ให้ทำงานต่อได้ทันทีโดยไม่เสียบริบท
อัปเดตล่าสุด: 2026-06-22

---

## 1. โปรเจกต์นี้คืออะไร
เกม 3rd-person ชื่อ **The After** ทำด้วย **Unity 6.5 (6000.5.0f1)** ใช้ **Universal Render Pipeline** และ **Input System ใหม่**
ตัวละครหลักคือ **Asher** เดินสำรวจฉากและคุยกับ NPC (ปัจจุบันมี **Logan**) ผ่านระบบบทสนทนา

โฟลเดอร์โปรเจกต์บนเครื่อง: `C:\Users\omgpo\THE AFTER`

---

## 2. วิธีตั้งค่า Claude Project ใหม่ (ทำในแอป Claude)
1. สร้าง Project ใหม่ในแอป Claude ตั้งชื่อเช่น "The After"
2. **เชื่อมโฟลเดอร์เดิม** `C:\Users\omgpo\THE AFTER` เข้ากับ session (ไฟล์ทั้งหมดอยู่บนดิสก์อยู่แล้ว ไม่ต้องคัดลอก)
3. แนบไฟล์นี้ (`PROJECT_HANDOFF.md`) เข้า Project knowledge หรือวางเนื้อหาลงใน custom instructions
4. ถ้าต้องการคุมหน้าจอ Unity ให้ขอสิทธิ์แอป "Unity" ทุก session ใหม่

> หมายเหตุ: ข้อจำกัด "คุมคอมไม่ครบทุกแอป" (เบราว์เซอร์คลิกไม่ได้, Terminal พิมพ์ไม่ได้) เป็นดีไซน์ด้านความปลอดภัย ไม่ใช่บั๊ก — Project ใหม่ก็เหมือนกัน

---

## 3. สคริปต์หลัก (ทั้งหมดอยู่ใน `Assets/Scripts/`)

### PlayerMovement.cs — ติดบน Asher
เดินด้วย WASD/ลูกศร (เคลื่อนที่สัมพันธ์กับกล้อง), Shift = วิ่ง, Space = กระโดด
ใช้ `CharacterController` + ขับ Animator param `Speed` และ trigger `Jump`
หยุดรับ input อัตโนมัติเมื่อ `DialogueManager.IsActive`

### CameraFollow.cs — ติดบน Main Camera
กล้องออร์บิทรอบ Asher (กดเมาส์ขวาลาก = หมุน, สกรอลล์ = ซูม)
มีโหมดโฟกัสตอนคุย: ล็อกกล้องไปที่ NPC โดยมี `dialogueDistance`, `dialogueHeight`, และ
`dialogueSideOffset` (เลื่อนกล้องไปด้านข้างตามแกน right ของ NPC ไม่ให้หัว Asher บัง)

### NPCInteractable.cs — ติดบน NPC (เช่น Logan)
เก็บ `npcName`, อาร์เรย์ `lines[]` (บทพูด), และ `interactRange`
**โหลดเสียงพากย์อัตโนมัติ**: ตอน Awake จะอ่าน AudioClip จาก `Resources/Voice` แล้วจับคู่กับแต่ละบรรทัด
โดยเทียบชื่อแบบ normalize (ตัดอักขระที่ไม่ใช่ตัวอักษร/ตัวเลขออก) → ไม่ต้องลากใส่ Inspector

### PlayerInteractor.cs — ติดบน Asher
หา NPC ที่ใกล้ที่สุดในระยะ, โชว์ hint "Press E", กด E เพื่อเริ่มบทสนทนา

### DialogueManager.cs — ติดบน DialogueManager (สร้าง UI ตอนรัน)
แสดงกล่องบทสนทนา + ชื่อผู้พูด, เลื่อนบรรทัดด้วยปุ่ม หรือ E/Enter/Space
มี **AudioSource** เล่นคลิปเสียงตรงบรรทัดทุกครั้งที่ขึ้นข้อความ และหยุดเมื่อจบ

### MainMenuUI.cs — เมนูหลัก (มีตั้งค่า MouseSensitivity ผ่าน PlayerPrefs)

---

## 4. ระบบเสียงพากย์ — วิธีเพิ่มบทพูด/เสียงใหม่
1. วางไฟล์ `.mp3`/`.wav` ใน `Assets/Resources/Voice/`
2. **ตั้งชื่อไฟล์ให้ตรงกับข้อความบทพูด** (เครื่องหมายวรรคตอนไม่ต้องเป๊ะ ระบบ normalize ให้)
   - เช่น บรรทัด `"Hey Asher. You're finally awake."` → ไฟล์ `Hey Asher. You're finally awake..mp3`
3. เพิ่มข้อความบรรทัดใน `lines[]` ของ NPCInteractable ตามลำดับ
4. ระบบจับคู่ + เล่นให้อัตโนมัติ ไม่ต้องแก้โค้ด

ปัจจุบัน Logan มี 2 บรรทัด พร้อมเสียงครบ:
- "Hey Asher. You're finally awake."
- "Eat something, then come find me. We have work to do."

---

## 5. สถานะปัจจุบัน / สิ่งที่ทำไปแล้ว
- ✅ ควบคุมตัวละครได้ครบ (WASD, หมุน, E, บทสนทนา, กล้องโฟกัส) — **ต้องคลิกที่ Game view ก่อน** คีย์บอร์ดถึงทำงาน (พฤติกรรมปกติของ Input System ใหม่)
- ✅ เพิ่ม `dialogueSideOffset` ให้กล้องตอนคุย
- ✅ เพิ่มระบบเสียงพากย์ Logan (โหลด+จับคู่+เล่นอัตโนมัติ) ทดสอบผ่านทั้ง 2 บรรทัด
- ✅ เพิ่ม fade in/out ให้กล่องบทสนทนา (DialogueManager.cs ใช้ `CanvasGroup` + coroutine) ปรับความเร็วได้ที่ฟิลด์ `fadeDuration` ใน Inspector (ดีฟอลต์ 0.25 วิ, ใส่ 0 = ตัดทันที)
- ✅ ลดระยะกด E ของ Logan เป็น 2 (interactRange) ต้องเข้าใกล้ขึ้นถึงจะขึ้น prompt
- ✅ เพิ่มระบบ "ขยับพูดคุย" แบบ procedural ให้ NPC (NPCInteractable.cs) — ขยับ bone หัว (พยัก/หัน) + แขน (UpperArm/LowerArm ซ้ายขวาสลับจังหวะ ข้อศอกงอ) + เอวบิดเบาๆ ทับบนท่า idle ขณะคุย หา bone หัว/Torso/แขน อัตโนมัติจากชื่อ ไม่ต้องลากใส่ Inspector
  - **ผูกกับ typewriter**: ขยับเฉพาะตอนข้อความกำลังพิมพ์ พอพิมพ์จบ = หยุด (อ่านสถานะจาก `DialogueManager.IsTyping`) กด Next/บรรทัดใหม่ = พิมพ์+ขยับใหม่จนจบบทพูด ปรับ idleGestureLevel > 0 ถ้าอยากให้ขยับต่อหลังพิมพ์จบ
  - ปรับได้ใน Inspector ของ Logan: headNodAmount, headTurnAmount, armGestureAmount, elbowGestureAmount, bodySwayAmount, talkSpeed, idleGestureLevel, talkBlendTime, speakBlendTime
  - rig เป็น Generic (animationType:2) เอาคลิปพูดสำเร็จรูป/Mixamo มาใส่ตรงๆ ไม่ได้ เลยใช้ procedural แทน
- ✅ เพิ่มเสียงเดิน/วิ่ง/กระโดด (PlayerMovement.cs) — ฝีเท้าเล่นเป็นจังหวะตอนเดิน (walkStepInterval) ถี่ขึ้นตอนวิ่ง/Shift (runStepInterval) สุ่มคลิป+pitch ไม่ให้ซ้ำ, มีเสียงกระโดดตอนกด Space และเสียงลงพื้นตอนแตะพื้น โหลดคลิปอัตโนมัติจาก `Resources/SFX` ตามชื่อ ปรับ volume/จังหวะได้ใน Inspector (footstepVolume, jumpVolume, landVolume, walkStepInterval, runStepInterval)
  - ไฟล์เสียงใน `Assets/Resources/SFX/` (เสียงสังเคราะห์แบบ simple): `Footstep_A.wav`, `Footstep_B.wav` (เดิน/วิ่ง สลับ 2 แบบ), `Jump.wav` (กระโดด) — เพิ่ม/แทนได้โดยวางไฟล์ที่ชื่อมีคำว่า step, jump, หรือ land ระบบจับให้เอง ปรับจังหวะ/ดังที่ Asher → Player Movement → Audio (Walk Step Interval ตอนนี้ 0.6, Run 0.4, Footstep Volume 1)
- ✅ เพิ่ม typewriter ให้กล่องบทสนทนา (DialogueManager.cs) — ข้อความขึ้นทีละตัวอักษร ปรับความเร็วที่ `typeSpeed` ใน Inspector (ตัว/วินาที, ดีฟอลต์ 35; 0 = ขึ้นทันที) กด Next ระหว่างพิมพ์ = เผยข้อความเต็มบรรทัดทันที, กดอีกที = ไปบรรทัดถัดไป
- ✅ เคลียร์ warning CS0618 แล้ว — แก้ `PlayerInteractor.cs` ให้ใช้ `FindObjectsByType<NPCInteractable>(FindObjectsInactive.Exclude)` (Unity 6.5 เลิกใช้ทุก overload ที่รับ `FindObjectsSortMode`) ตอนนี้ Console = 0/0/0

---

- ✅ กระโดด = พุ่งไปข้างหน้า (PlayerMovement.cs) เพราะ jump anim เป็นท่า Roll — เพิ่มแรงพุ่งตามทิศที่หันอยู่ (rollForwardSpeed ดีฟอลต์ 4) จางด้วย rollDamping มี **rollDelay** (ดีฟอลต์ 0.35 วิ) หน่วงก่อนเริ่มพุ่งหลังกดกระโดด เพื่อให้ตรงกับจังหวะ animation roll (เพิ่ม rollDelay = พุ่งช้าลง/ใกล้ตอนลงพื้นมากขึ้น) ปรับทั้งหมดได้ใน Inspector
- ✅ เพิ่ม Pause menu (PauseMenu.cs) — กด Esc หยุดเกม (Time.timeScale=0 + ปิดเสียง) โชว์ overlay "PAUSED" + ปุ่ม RESUME / MAIN MENU / QUIT สไตล์เดียวกับเมนูหลัก สร้าง UI ตอนรันเอง และ bootstrap ตัวเองเมื่อเข้าฉากที่มี PlayerMovement (ไม่ต้องตั้งใน Inspector)
  - ⚠️ Esc ใช้ได้ใน build แต่ **ใน Unity Editor กด Esc ไม่ติด** เพราะ Game view กลืนปุ่ม Esc (พฤติกรรมปกติของ editor) — เทสต์ pause เต็มๆ ต้อง build หรือชั่วคราวผูกปุ่มอื่น
- ✅ แก้บั๊กคลิกปุ่ม UI ด้วยเมาส์ไม่ติด — EventSystem ที่สร้างตอนรันไม่มี input actions ทำให้คลิกปุ่ม (เช่นปุ่ม Next ในบทสนทนา, ปุ่ม pause) ไม่ทำงาน แก้โดยเรียก `AssignDefaultActions()` ใน DialogueManager/PauseMenu ตอนสร้าง EventSystem
- ✅ วางฟอนต์ `NewTegomin-Regular.ttf` ที่ `Assets/Resources/Fonts/` (มี `.meta` เดิมรออยู่แล้ว ไม่ต้อง re-import) — `GameFont.cs` โหลดให้ทุก UI ที่สร้างจากโค้ดอัตโนมัติ
- ✅ ปรับสกิน `PauseMenu.cs` ให้เป็นการ์ดกระดาษเก่า (parchment beige #D4BC8A + ขอบหมึกเข้ม #1C2433 + ปุ่มเอียงเล็กน้อยให้ดูทำมือ + ปุ่มปิดวงกลมส้มมุมขวาบน) ให้ตรงกับ stat-card UI ที่ออกแบบไว้ก่อนหน้า — logic เดิม (resume/main menu/quit, Esc, Time.timeScale) ไม่เปลี่ยน


## 6. ไอเดียงานต่อ (ถ้าต้องการ)
- เพิ่ม NPC และบทพูดใหม่
- เพิ่ม typewriter effect (ตัวอักษรไล่ขึ้นทีละตัว) ให้บทสนทนา
- ทำให้ Game view รับ input โดยไม่ต้องคลิกก่อน (เปิด Maximize On Play)
