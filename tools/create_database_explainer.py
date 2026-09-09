from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import subprocess, sys, textwrap, asyncio

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "database-explainer"
OUT.mkdir(parents=True, exist_ok=True)
W, H, FPS = 1920, 1080, 30

def font(size, bold=False):
    names = ["C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf",
             "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"]
    for name in names:
        if Path(name).exists(): return ImageFont.truetype(name, size)
    return ImageFont.load_default()

F_TITLE, F_SUB, F_BODY, F_SMALL = font(54, True), font(31, True), font(27), font(22)
BG, PANEL, BLUE, CYAN, GREEN, AMBER, RED, WHITE, MUTED = "#09111f", "#111d30", "#4285f4", "#25c2d7", "#3dcc8e", "#ffbf47", "#ff6978", "#f5f8ff", "#aebbd0"

def base(title, kicker):
    im = Image.new("RGB", (W,H), BG); d=ImageDraw.Draw(im)
    d.rounded_rectangle((65,45,W-65,H-45), 34, fill=PANEL, outline="#243653", width=3)
    d.text((110,78), kicker.upper(), font=F_SMALL, fill=CYAN)
    d.text((110,115), title, font=F_TITLE, fill=WHITE)
    d.line((110,195,W-110,195), fill="#2a3b58", width=2)
    return im,d

def box(d, xy, title, lines, color=BLUE, width=420):
    x,y=xy; wrapped=[]
    for line in lines: wrapped += textwrap.wrap(line, width=max(18,int(width/15))) or [""]
    h=88+42*len(wrapped)
    d.rounded_rectangle((x,y,x+width,y+h), 22, fill="#172943", outline=color, width=4)
    d.rectangle((x,y,x+width,y+12), fill=color)
    title_font=F_SUB
    while d.textbbox((0,0),title,font=title_font)[2] > width-45 and title_font.size > 18:
        title_font=font(title_font.size-2, True)
    d.text((x+24,y+28), title, font=title_font, fill=WHITE)
    yy=y+76
    for line in wrapped:
        d.text((x+25,yy), "• "+line, font=F_SMALL, fill=MUTED); yy+=40
    return (x,y,x+width,y+h)

def arrow(d, a, b, color=CYAN, label=None):
    d.line((a,b), fill=color, width=7)
    import math
    ang=math.atan2(b[1]-a[1],b[0]-a[0]); s=20
    for off in (2.55,-2.55):
        p=(b[0]+s*math.cos(ang+off),b[1]+s*math.sin(ang+off)); d.line((b,p),fill=color,width=7)
    if label: d.text(((a[0]+b[0])//2-60,(a[1]+b[1])//2-38),label,font=F_SMALL,fill=color)

def footer(d,n):
    d.text((110,H-92),f"Identity database explained  •  {n}/10",font=F_SMALL,fill="#71839e")

slides=[]
def add(title,kicker,drawfn):
    im,d=base(title,kicker); drawfn(d); footer(d,len(slides)+1)
    p=OUT/f"slide_{len(slides)+1:02}.png"; im.save(p); slides.append(p)

add("The big picture", "Start here", lambda d: (
    box(d,(150,285),"WHO?",["UserAccount — Alice's master record"],CYAN,470),
    box(d,(725,285),"CAN ENTER?",["UserApplication — access to HR"],BLUE,470),
    box(d,(1300,285),"CAN DO WHAT?",["Roles + permissions"],GREEN,470),
    d.text((300,650),"Authentication proves identity.  Authorization decides access.",font=font(42,True),fill=WHITE)
))

def s2(d):
    b=box(d,(710,250),"UserAccount (PARENT)",["UserId 101","EMP001 — Alice","Active account"],CYAN,500)
    for x,t,c in [(90,"UserCredential",BLUE),(550,"Device",GREEN),(1010,"UserMfaMethod",AMBER),(1470,"UserApplication",RED)]:
        box(d,(x,650),t,["UserId = 101"],c,360); arrow(d,((b[0]+b[2])//2,b[3]),(x+180,650),c)
add("User tables: parent to children", "Architecture", s2)

def s3(d):
    box(d,(100,260),"UserAccount",["101 | EMP001 | Alice | Active"],CYAN,520)
    box(d,(700,260),"UserCredential",["501 | User 101 | Password","Stores hash, never password"],BLUE,520)
    box(d,(1300,260),"Device",["301 | User 101 | Alice Laptop","Trusted = Yes"],GREEN,520)
    arrow(d,(620,390),(700,390)); arrow(d,(1220,390),(1300,390))
    d.text((240,665),"One Alice",font=font(38,True),fill=CYAN); d.text((780,665),"Credential history",font=font(38,True),fill=BLUE); d.text((1400,665),"Many devices",font=font(38,True),fill=GREEN)
add("Identity and authentication tables", "Alice example", s3)

def s4(d):
    box(d,(120,300),"UserAccount",["Alice — UserId 101"],CYAN,430)
    box(d,(745,300),"UserApplication",["User 101 + App 10","Active = Yes"],BLUE,430)
    box(d,(1370,300),"Application",["App 10 — HR Management"],GREEN,430)
    arrow(d,(550,410),(745,410),label="assigned"); arrow(d,(1175,410),(1370,410))
    d.text((335,700),"No UserApplication row = Alice cannot enter that application",font=font(39,True),fill=AMBER)
add("The access gate: UserApplication", "Authorization", s4)

def s5(d):
    box(d,(80,260),"Application",["HR Management"],CYAN,360)
    box(d,(555,260),"Module",["Employee Management"],BLUE,400)
    box(d,(1090,210),"Capabilities",["EMPLOYEE_VIEW","EMPLOYEE_CREATE","EMPLOYEE_DELETE"],GREEN,600)
    arrow(d,(440,365),(555,365)); arrow(d,(955,365),(1090,365))
    d.text((270,750),"Application → Module → Capability",font=font(50,True),fill=WHITE)
add("What actions exist in the application?", "Permission catalog", s5)

def s6(d):
    box(d,(80,310),"Alice",["UserId 101"],CYAN,330)
    box(d,(515,310),"UserRole",["Alice has HR Viewer"],BLUE,420)
    box(d,(1040,310),"RolePermission",["HR Viewer grants VIEW"],GREEN,480)
    box(d,(1610,310),"Capability",["EMPLOYEE_VIEW"],AMBER,250)
    arrow(d,(410,415),(515,415)); arrow(d,(935,415),(1040,415)); arrow(d,(1520,415),(1610,415))
    d.text((350,735),"Result: Alice inherits EMPLOYEE_VIEW from her role",font=font(42,True),fill=GREEN)
add("How Alice receives role permissions", "Role path", s6)

def s7(d):
    box(d,(120,260),"Role says",["VIEW = Allow"],BLUE,430)
    box(d,(745,260),"Alice override",["CREATE = Allow","DELETE = Deny"],AMBER,430)
    box(d,(1370,260),"Final result",["VIEW ✓","CREATE ✓","DELETE ✕"],GREEN,430)
    arrow(d,(550,410),(745,410)); arrow(d,(1175,410),(1370,410))
    d.text((380,745),"Priority: Deny override → Allow override → Role",font=font(45,True),fill=WHITE)
add("Special permissions for one user", "Overrides", s7)

def s8(d):
    items=[("1  UserAccount","Find Alice",CYAN),("2  UserCredential","Verify password",BLUE),("3  UserApplication","Check HR access",GREEN),("4  MFA Challenge","Verify OTP",AMBER),("5  RefreshToken","Create session",RED),("6  Audit","Record success",CYAN)]
    xs=[80,380,680,980,1280,1580]
    for i,(t,l,c) in enumerate(items):
        box(d,(xs[i],310),t,[l],c,260)
        if i<5: arrow(d,(xs[i]+260,420),(xs[i+1],420),c)
    d.text((410,735),"Alice + password + OTP → authenticated HR session",font=font(43,True),fill=WHITE)
add("Login flow — complete example", "Step by step", s8)

def s9(d):
    items=[("User active?","YES",CYAN),("HR access?","YES",BLUE),("Deny override?","NO",AMBER),("Allow override?","NO",AMBER),("Role grants VIEW?","YES",GREEN),("ALLOW","✓",GREEN)]
    y=245
    for i,(q,a,c) in enumerate(items):
        d.rounded_rectangle((350,y,1570,y+92),18,fill="#172943",outline=c,width=3)
        d.text((390,y+24),q,font=F_SUB,fill=WHITE); d.text((1370,y+24),a,font=F_SUB,fill=c)
        if i<len(items)-1: arrow(d,(960,y+92),(960,y+120),c)
        y+=120
add("Permission check: Can Alice view employees?", "Request flow", s9)

def s10(d):
    d.text((150,245),"USER / SECURITY",font=F_SUB,fill=CYAN)
    d.text((725,245),"APPLICATION ACCESS",font=F_SUB,fill=BLUE)
    d.text((1320,245),"ORGANIZATION",font=F_SUB,fill=GREEN)
    box(d,(120,300),"UserAccount",["Credential","Device","MFA","Token","Audit"],CYAN,470)
    box(d,(700,300),"UserApplication",["UserRole → RolePermission","UserPermissionOverride"],BLUE,530)
    box(d,(1300,300),"OrganizationUnit",["Country → Region → State","Branch → Location","Department → Team → Alice"],GREEN,500)
    d.text((190,785),"Who?",font=font(40,True),fill=CYAN); d.text((830,785),"What can they do?",font=font(40,True),fill=BLUE); d.text((1420,785),"Where do they belong?",font=font(40,True),fill=GREEN)
add("The complete database, in three questions", "Recap", s10)

narrations = [
"Welcome. We will understand the user module database using one simple example: Alice, employee EMP zero zero one. First remember two questions. Authentication proves who Alice is. Authorization decides which application she can enter and what actions she can perform.",
"User Account is the main parent table. Alice has User ID one hundred and one. Child tables reuse that User ID. User Credential stores her password hash. Device stores her laptop and phone. User MFA Method stores her OTP methods. User Application stores which applications Alice may access.",
"User Account contains Alice's main identity and account status. User Credential contains password or PIN history; plain passwords are never stored. Device contains every registered device. One user can therefore have multiple credentials over time and multiple devices.",
"User Application is the access gate. This row joins Alice, user one hundred and one, to HR application ten. If this active row does not exist, Alice cannot enter HR, even when roles exist elsewhere in the database.",
"Inside an application, actions are organized from application to module to capability. HR contains the Employee Management module. That module contains capabilities such as view, create, and delete employee. A capability is the smallest permission checked by the API.",
"Alice receives normal permissions through roles. User Role says Alice has the HR Viewer role. Role Permission says HR Viewer grants Employee View. Following the relationship from Alice through both bridge tables reaches the capability she is allowed to use.",
"User Permission Override handles exceptions for one person. Alice gets a special allow for Employee Create and a deny for Employee Delete. The decision priority is simple. Deny override wins first. Then allow override. If no override exists, use the role permissions.",
"Now follow Alice's login. First find her User Account and confirm it is active. Second verify the password against User Credential. Third check the active User Application row for HR. Fourth verify her MFA challenge. Fifth issue a Refresh Token for her session. Finally, write Login Succeeded to Authentication Audit.",
"When Alice opens View Employees, the API checks in order. Is Alice active? Yes. Does she have active HR access? Yes. Is there a deny override for Employee View? No. Is there an allow override? No. Does her role grant Employee View? Yes. The final decision is allow.",
"Here is the complete structure in three questions. User Account and its security children answer: who is the user? User Application, roles, capabilities, and overrides answer: what can the user do? Organization Unit, Department, and Team answer: where does the user belong? That is the full user database from parent to child."
]

sys.path.insert(0,str(ROOT/".video-deps"))
import edge_tts
import imageio_ffmpeg

async def make_audio():
    files=[]
    for i,script in enumerate(narrations,1):
        audio=OUT/f"audio_{i:02}.mp3"
        await edge_tts.Communicate(script, "en-IN-NeerjaNeural", rate="-5%").save(str(audio))
        files.append(audio)
    return files

audios=asyncio.run(make_audio())
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
segments=[]
for i,(png,audio) in enumerate(zip(slides,audios),1):
    seg=OUT/f"segment_{i:02}.mp4"
    subprocess.run([ffmpeg,"-y","-loop","1","-i",str(png),"-i",str(audio),"-c:v","libx264","-tune","stillimage","-pix_fmt","yuv420p","-r",str(FPS),"-c:a","aac","-b:a","160k","-af","apad=pad_dur=0.7","-shortest",str(seg)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    segments.append(seg)
concat=OUT/"concat.txt"; concat.write_text("\n".join(f"file '{p.as_posix()}'" for p in segments),encoding="utf-8")
final=OUT/"identity-database-explained.mp4"
subprocess.run([ffmpeg,"-y","-f","concat","-safe","0","-i",str(concat),"-c","copy",str(final)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
print(final)
