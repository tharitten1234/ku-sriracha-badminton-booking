using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddSingleton<Db>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath,".local","keys")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
 o.Cookie.Name="ku-court-session"; o.Cookie.HttpOnly=true; o.Cookie.SameSite=SameSiteMode.Strict; o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest; o.ExpireTimeSpan=TimeSpan.FromHours(4);
 o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401; return c.Response.WriteAsJsonAsync(new {message="กรุณาเข้าสู่ระบบก่อน"});};
 o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403; return c.Response.WriteAsJsonAsync(new {message="ไม่มีสิทธิ์เข้าถึง"});};
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("login",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=20,Window=TimeSpan.FromMinutes(1)}));});
var app=builder.Build();
app.Use(async(c,next)=>{
 c.Response.Headers["X-Content-Type-Options"]="nosniff";
 c.Response.Headers["Referrer-Policy"]="same-origin";
 if(c.Request.Method is "POST" or "PUT" or "DELETE") {
  var origin=c.Request.Headers.Origin.ToString();
  // Vite dev proxy and deployed single-origin app are both supported.
  if(origin.Length>0 && (!Uri.TryCreate(origin,UriKind.Absolute,out var u)||u.Authority!=c.Request.Host.Value)) {c.Response.StatusCode=403;await c.Response.WriteAsJsonAsync(new{message="คำขอจากเว็บไซต์อื่นไม่ได้รับอนุญาต"});return;}
 }
 try {await next();}
 catch(SqlException e){
  app.Logger.LogWarning("SQL error {Number}",e.Number);
  if(!c.Response.HasStarted){c.Response.StatusCode=e.Number>=50000||e.Number==1205?409:503;var english=c.Request.Headers.AcceptLanguage.ToString().StartsWith("en",StringComparison.OrdinalIgnoreCase);var localized=e.Number switch{50001=>english?"The facility is closed on the selected date":"วันที่เลือกสนามปิดให้บริการ กรุณาตรวจสอบกิจกรรม",50002=>english?"Choose two different students":"กรุณาใช้รหัสนิสิตคนละคน",50003=>english?"A student ID is invalid or inactive":"ไม่พบรหัสนิสิตหรือรหัสนิสิตไม่มีสิทธิ์ใช้งาน",50004=>english?"The court or time is unavailable":"คอร์ดหรือช่วงเวลาปิดให้บริการ",50005=>english?"This time slot is already booked":"ช่วงเวลานี้ถูกจองแล้ว กรุณาเลือกเวลาอื่น",50006=>english?"Booking not found":"ไม่พบการจอง",50009=>english?"Choose a date within the next 30 days":"เลือกวันจองได้ตั้งแต่วันนี้ถึง 30 วันข้างหน้า",50010=>english?"An account already exists for this student ID. Please sign in.":"รหัสนี้มีบัญชีแล้ว กรุณาเข้าสู่ระบบ",_=>english?"The request could not be completed":"ทำรายการไม่สำเร็จ"};await c.Response.WriteAsJsonAsync(new{message=e.Number>=50000?localized:e.Number==1205?(english?"A booking was made at the same time. Please retry.":"มีการจองพร้อมกัน กรุณาลองอีกครั้ง"):(english?"Could not connect to the database":"เชื่อมต่อฐานข้อมูลไม่ได้ กรุณาตรวจสอบ SQL Server")});}
 }
 catch(BadHttpRequestException){c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new{message="ข้อมูลที่ส่งมาไม่ถูกต้อง"});}
});
app.UseDefaultFiles();app.UseStaticFiles();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
var db=app.Services.GetRequiredService<Db>();
var seedPassword=builder.Configuration["Demo:AdminPassword"];
if(!string.IsNullOrWhiteSpace(seedPassword)){
 if(seedPassword.Length<12)throw new InvalidOperationException("Admin password must have at least 12 characters");
 var hash=Passwords.Hash(seedPassword);
 if(builder.Configuration.GetValue<bool>("Demo:ResetAdminPassword"))await db.Query("IF EXISTS(SELECT 1 FROM dbo.STAFF_USERS WHERE Username='admin') UPDATE dbo.STAFF_USERS SET PasswordHash=@Hash,IsActive=1,Role='ADMIN' WHERE Username='admin'; ELSE INSERT dbo.STAFF_USERS(Username,PasswordHash) VALUES('admin',@Hash)",new SqlParameter("@Hash",hash));
 else await db.Query("IF NOT EXISTS(SELECT 1 FROM dbo.STAFF_USERS WHERE Username='admin') INSERT dbo.STAFF_USERS(Username,PasswordHash) VALUES('admin',@Hash)",new SqlParameter("@Hash",hash));
}
app.MapGet("/api/health",async(Db d)=>{await d.Query("SELECT 1 Ok");return Results.Ok(new{status="ok",database="SQL Server"});});
app.MapGet("/api/today",async(Db d)=>Results.Ok(new{
 schedule=(await d.Query("SELECT CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE) Today,s.ScheduleID,s.IsCourtOpen,s.ActivityName,s.Description FROM (SELECT 1 n)x LEFT JOIN dbo.FACILITY_SCHEDULE s ON s.ScheduleDate=CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE)"))[0],
 slots=await d.Query("SELECT * FROM dbo.vw_TodayCourtSchedule ORDER BY CourtID,SlotID")
}));
app.MapGet("/api/availability/{date}",async(string date,Db d)=>{
 if(!DateOnly.TryParseExact(date,"yyyy-MM-dd",out var selected))return Results.BadRequest(new{message="Choose a valid date"});
 var todayRows=await d.Query("SELECT CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE) Today");
 var today=DateOnly.FromDateTime((DateTime)todayRows[0]["today"]!);
 if(selected<today||selected>today.AddDays(30))return Results.BadRequest(new{message="Choose a date within the next 30 days"});
 var day=selected.ToDateTime(TimeOnly.MinValue);
 var schedule=await d.Query("SELECT @Date Today,s.ScheduleID,s.IsCourtOpen,s.ActivityName,s.Description FROM (SELECT 1 n)x LEFT JOIN dbo.FACILITY_SCHEDULE s ON s.ScheduleDate=@Date",new SqlParameter("@Date",day));
 var slots=await d.Query("SELECT c.CourtID,c.CourtName,c.CourtStatus,t.SlotID,CONVERT(VARCHAR(5),t.StartTime,108) StartTime,CONVERT(VARCHAR(5),t.EndTime,108) EndTime,@Date BookingDate,CASE WHEN s.ScheduleID IS NULL OR s.IsCourtOpen=0 OR NULLIF(LTRIM(RTRIM(s.ActivityName)),'') IS NOT NULL OR c.CourtStatus<>'ACTIVE' OR t.IsActive=0 THEN 'CLOSED' WHEN b.BookingID IS NOT NULL THEN 'BOOKED' ELSE 'AVAILABLE' END Availability FROM dbo.COURTS c CROSS JOIN dbo.TIME_SLOTS t LEFT JOIN dbo.FACILITY_SCHEDULE s ON s.ScheduleDate=@Date LEFT JOIN dbo.BOOKINGS b ON b.CourtID=c.CourtID AND b.SlotID=t.SlotID AND b.BookingDate=@Date AND b.Status='BOOKED'",new SqlParameter("@Date",day));
 return Results.Ok(new{schedule=schedule[0],slots});
});
app.MapPost("/api/students/register",async(StudentRegistration p,Db d)=>{
 if(!StudentId.Valid(p.StudentID))return Results.BadRequest(new{message="Student ID must contain exactly 10 digits"});
 if(string.IsNullOrWhiteSpace(p.FullName)||p.FullName.Trim().Length>100)return Results.BadRequest(new{message="Enter your name (up to 100 characters)"});
 if(string.IsNullOrEmpty(p.Password)||p.Password.Length<10||p.Password.Length>128)return Results.BadRequest(new{message="Password must be 10 to 128 characters"});
 // Lock the ID range so parallel registrations cannot claim the same ID.
 await d.Query("SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN; IF EXISTS(SELECT 1 FROM dbo.STUDENTS WITH(UPDLOCK,HOLDLOCK) WHERE StudentID=@I AND PasswordHash IS NOT NULL) THROW 50010,N'An account already exists for this student ID. Please sign in.',1; IF EXISTS(SELECT 1 FROM dbo.STUDENTS WHERE StudentID=@I) UPDATE dbo.STUDENTS SET FullName=@N,Email=NULL,PasswordHash=@H,IsActive=1 WHERE StudentID=@I; ELSE INSERT dbo.STUDENTS(StudentID,FullName,PasswordHash,IsActive) VALUES(@I,@N,@H,1); COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH",new SqlParameter("@I",p.StudentID),new SqlParameter("@N",p.FullName.Trim()),new SqlParameter("@H",Passwords.Hash(p.Password)));
 return Results.Ok(new{message="Account created. You can now sign in."});
}).RequireRateLimiting("login");
app.MapPost("/api/students/login",async(StudentLogin p,HttpContext c,Db d)=>{
 if(!StudentId.Valid(p.StudentID)||string.IsNullOrEmpty(p.Password)||p.Password.Length>128)return Results.BadRequest(new{message="Enter a 10-digit student ID and password"});
 var rows=await d.Query("SELECT StudentID,FullName,PasswordHash FROM dbo.STUDENTS WHERE StudentID=@I AND IsActive=1",new SqlParameter("@I",p.StudentID));
 if(rows.Count!=1||rows[0]["passwordHash"] is not string hash||!Passwords.Verify(p.Password,hash))return Results.Json(new{message="Student ID or password is incorrect"},statusCode:401);
 await c.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,p.StudentID),new Claim(ClaimTypes.Role,"STUDENT"),new Claim("StudentID",p.StudentID),new Claim("FullName",(string)rows[0]["fullName"]!)},CookieAuthenticationDefaults.AuthenticationScheme)));
 return Results.Ok(new{message="Signed in"});
}).RequireRateLimiting("login");
app.MapGet("/api/session",(HttpContext c)=>Results.Ok(new{authenticated=c.User.Identity?.IsAuthenticated??false,role=c.User.FindFirstValue(ClaimTypes.Role),studentId=c.User.FindFirstValue("StudentID"),fullName=c.User.FindFirstValue("FullName"),username=c.User.Identity?.Name}));
app.MapPost("/api/students/partner",async(PartnerRequest p,HttpContext c,Db d)=>{
 if(!c.User.IsInRole("STUDENT"))return Results.Unauthorized();
 if(!StudentId.Valid(p.StudentID2)||p.StudentID2==c.User.FindFirstValue("StudentID"))return Results.BadRequest(new{message="Enter a different student's 10-digit ID"});
 var rows=await d.Query("SELECT StudentID,FullName,dbo.fn_IsStudentEligible(StudentID) Eligible FROM dbo.STUDENTS WHERE StudentID=@I",new SqlParameter("@I",p.StudentID2));
 if(rows.Count!=1||!(bool)rows[0]["eligible"]!)return Results.BadRequest(new{message="The second student must register an active account first"});
 return Results.Ok(rows[0]);
}).RequireAuthorization().RequireRateLimiting("login");
app.MapPost("/api/bookings",async(BookingRequest p,HttpContext c,Db d)=>{
 if(!c.User.IsInRole("STUDENT"))return Results.Forbid();
 if(p.CourtID<1||p.SlotID<1)return Results.BadRequest(new{message="กรุณาเลือกคอร์ดและช่วงเวลา"});
 if(p.StudentID2 is null||!StudentId.Valid(p.StudentID2))return Results.BadRequest(new{message="Enter the second student's 10-digit ID"});
 return Results.Ok((await d.Procedure("sp_CreateBooking",new SqlParameter("@CourtID",p.CourtID),new SqlParameter("@SlotID",p.SlotID),new SqlParameter("@BookingDate",p.BookingDate.ToDateTime(TimeOnly.MinValue)),new SqlParameter("@StudentID1",c.User.FindFirstValue("StudentID")),new SqlParameter("@StudentID2",p.StudentID2)))[0]);
}).RequireAuthorization();
app.MapGet("/api/bookings",async(HttpContext c,Db d)=>Results.Ok(await d.Query(c.User.IsInRole("ADMIN")?"SELECT * FROM dbo.vw_BookingDetails ORDER BY BookingID DESC":"SELECT * FROM dbo.vw_BookingDetails WHERE StudentID1=@A OR StudentID2=@A ORDER BY BookingID DESC",new SqlParameter("@A",c.User.FindFirstValue("StudentID"))))).RequireAuthorization();
app.MapPost("/api/bookings/{id:int}/cancel",async(int id,HttpContext c,Db d)=>{
 if(!await Owns(id,c,d))return Results.NotFound(new{message="ไม่พบการจองหรือไม่มีสิทธิ์เข้าถึง"});
 return Results.Ok((await d.Procedure("sp_CancelBooking",new SqlParameter("@BookingID",id)))[0]);
}).RequireAuthorization();
app.MapPost("/api/bookings/{id:int}/donations",async(int id,DonationRequest p,HttpContext c,Db d)=>{
 if(p.Amount<=0||p.Amount>100000||decimal.Round(p.Amount,2)!=p.Amount)return Results.BadRequest(new{message="กรุณาระบุเงิน 0.01–100,000 บาท ทศนิยมไม่เกิน 2 ตำแหน่ง"});
 if(!await Owns(id,c,d))return Results.NotFound(new{message="ไม่พบการจองหรือไม่มีสิทธิ์เข้าถึง"});
 return Results.Ok((await d.Procedure("sp_AddDonation",new SqlParameter("@BookingID",id),new SqlParameter("@Amount",SqlDbType.Decimal){Precision=10,Scale=2,Value=p.Amount}))[0]);
}).RequireAuthorization();
app.MapPost("/api/admin/login",async(LoginRequest p,HttpContext c,Db d)=>{
 if(string.IsNullOrEmpty(p.Username)||string.IsNullOrEmpty(p.Password)||p.Password.Length>255)return Results.BadRequest(new{message="กรุณากรอกชื่อผู้ใช้และรหัสผ่าน"});
 var rows=await d.Query("SELECT StaffID,Username,PasswordHash FROM dbo.STAFF_USERS WHERE Username=@U AND IsActive=1 AND Role='ADMIN'",new SqlParameter("@U",p.Username));
 if(rows.Count!=1||!Passwords.Verify(p.Password,(string)rows[0]["passwordHash"]!))return Results.Json(new{message="ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง"},statusCode:401);
 await c.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,(string)rows[0]["username"]!),new Claim(ClaimTypes.Role,"ADMIN")},CookieAuthenticationDefaults.AuthenticationScheme)));
 return Results.Ok(new{message="เข้าสู่ระบบแล้ว"});
}).RequireRateLimiting("login");
app.MapPost("/api/logout",async(HttpContext c)=>{await c.SignOutAsync();return Results.Ok(new{message="ออกจากระบบแล้ว"});});
var admin=app.MapGroup("/api/admin").RequireAuthorization(p=>p.RequireRole("ADMIN"));
admin.MapGet("/schedule",async(Db d)=>Results.Ok(await d.Query("SELECT * FROM dbo.FACILITY_SCHEDULE ORDER BY ScheduleDate DESC")));
admin.MapPut("/schedule",async(ScheduleRequest p,Db d)=>{
 if(p.ActivityName?.Length>200||p.Description?.Length>500)return Results.BadRequest(new{message="ชื่อกิจกรรมหรือรายละเอียดมีความยาวเกินกำหนด"});
 if(p.IsCourtOpen&&!string.IsNullOrWhiteSpace(p.ActivityName))return Results.BadRequest(new{message="วันที่มีกิจกรรมต้องปิดการจองสนาม"});
 await d.Query("SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN; UPDATE dbo.FACILITY_SCHEDULE WITH(UPDLOCK,SERIALIZABLE) SET IsCourtOpen=@O,ActivityName=@A,Description=@D WHERE ScheduleDate=@Date; IF @@ROWCOUNT=0 INSERT dbo.FACILITY_SCHEDULE(ScheduleDate,IsCourtOpen,ActivityName,Description) VALUES(@Date,@O,@A,@D); COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH",new SqlParameter("@Date",p.ScheduleDate.ToDateTime(TimeOnly.MinValue)),new SqlParameter("@O",p.IsCourtOpen),new SqlParameter("@A",string.IsNullOrWhiteSpace(p.ActivityName)?null:p.ActivityName),new SqlParameter("@D",p.Description));
 return Results.Ok(new{message="บันทึกตารางเปิด–ปิดแล้ว"});
});
admin.MapPut("/courts/{id:int}",async(int id,CourtRequest p,Db d)=>{
 if(p.CourtStatus is not("ACTIVE" or "MAINTENANCE" or "CLOSED"))return Results.BadRequest(new{message="สถานะคอร์ดไม่ถูกต้อง"});
 var rows=await d.Query("UPDATE dbo.COURTS SET CourtStatus=@S OUTPUT inserted.CourtID WHERE CourtID=@I",new SqlParameter("@S",p.CourtStatus),new SqlParameter("@I",id));
 return rows.Count==0?Results.NotFound():Results.Ok(new{message="บันทึกสถานะคอร์ดแล้ว"});
});
admin.MapGet("/logs",async(Db d)=>Results.Ok(await d.Query("SELECT TOP(200) * FROM dbo.BOOKING_LOGS ORDER BY LogID DESC")));
admin.MapGet("/reports",async(string? from,string? to,Db d)=>{
 var todayRows=await d.Query("SELECT CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE) Today");
 var today=DateOnly.FromDateTime((DateTime)todayRows[0]["today"]!);
 var start=DateOnly.TryParseExact(from,"yyyy-MM-dd",out var f)?f:today.AddDays(-29);
 var end=DateOnly.TryParseExact(to,"yyyy-MM-dd",out var t)?t:today;
 if(end<start||end.DayNumber-start.DayNumber>365)return Results.BadRequest(new{message="Choose a valid report range of up to 366 days"});
 var p=new[]{new SqlParameter("@From",start.ToDateTime(TimeOnly.MinValue)),new SqlParameter("@To",end.ToDateTime(TimeOnly.MinValue))};
 return Results.Ok(new{
  totals=(await d.Query("SELECT COUNT(*) BookingCount,COALESCE(SUM(CASE WHEN BookingDate=CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE) AND Status='BOOKED' THEN 1 ELSE 0 END),0) TodayBookingCount,(SELECT COALESCE(SUM(Amount),0) FROM dbo.DONATIONS) DonationTotal FROM dbo.BOOKINGS"))[0],
  courts=await d.Query("SELECT c.CourtID,c.CourtName,COUNT(b.BookingID) BookingCount FROM dbo.COURTS c LEFT JOIN dbo.BOOKINGS b ON b.CourtID=c.CourtID AND b.Status='BOOKED' GROUP BY c.CourtID,c.CourtName ORDER BY BookingCount DESC,c.CourtID"),
  mostUsed=await d.Query("SELECT c.CourtName,COUNT(*) BookingCount FROM dbo.COURTS c INNER JOIN dbo.BOOKINGS b ON b.CourtID=c.CourtID WHERE b.Status='BOOKED' GROUP BY c.CourtName HAVING COUNT(*)>=1 ORDER BY BookingCount DESC"),
  daily=await d.Query("WITH Dates AS(SELECT @From ReportDate UNION ALL SELECT DATEADD(DAY,1,ReportDate) FROM Dates WHERE ReportDate<@To),B AS(SELECT BookingDate,COUNT(CASE WHEN Status='BOOKED' THEN 1 END) BookedCount,COUNT(CASE WHEN Status='CANCELLED' THEN 1 END) CancelledCount,COUNT(DISTINCT CASE WHEN Status='BOOKED' THEN CourtID END) CourtCount FROM dbo.BOOKINGS WHERE BookingDate BETWEEN @From AND @To GROUP BY BookingDate),D AS(SELECT DonationDay,DonationCount,TotalAmount FROM dbo.vw_DailyDonationSummary WHERE DonationDay BETWEEN @From AND @To) SELECT Dates.ReportDate,COALESCE(B.BookedCount,0) BookedCount,COALESCE(B.CancelledCount,0) CancelledCount,COALESCE(B.CourtCount,0) CourtCount,COALESCE(D.DonationCount,0) TipCount,COALESCE(D.TotalAmount,0) TipTotal FROM Dates LEFT JOIN B ON B.BookingDate=Dates.ReportDate LEFT JOIN D ON D.DonationDay=Dates.ReportDate ORDER BY Dates.ReportDate DESC OPTION(MAXRECURSION 366)",p)
 });
});
app.MapFallbackToFile("index.html");app.Run();

static async Task<bool> Owns(int id,HttpContext c,Db d)=>(await d.Query(c.User.IsInRole("ADMIN")?"SELECT BookingID FROM dbo.BOOKINGS WHERE BookingID=@I":"SELECT BookingID FROM dbo.BOOKINGS WHERE BookingID=@I AND (StudentID1=@A OR StudentID2=@A)",new SqlParameter("@I",id),new SqlParameter("@A",c.User.FindFirstValue("StudentID")))).Count>0;
record StudentRegistration(string StudentID,string FullName,string Password);
record StudentLogin(string StudentID,string Password);
record PartnerRequest(string StudentID2);
record BookingRequest(int CourtID,int SlotID,DateOnly BookingDate,string? StudentID2);
record DonationRequest(decimal Amount);
record LoginRequest(string Username,string Password);
record ScheduleRequest(DateOnly ScheduleDate,bool IsCourtOpen,string? ActivityName,string? Description);
record CourtRequest(string CourtStatus);
static class StudentId{public static bool Valid(string? id)=>id is {Length:10}&&id.All(char.IsAsciiDigit);}
static class Passwords{
 public static string Hash(string p){var salt=RandomNumberGenerator.GetBytes(16);return $"pbkdf2$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(p,salt,210000,HashAlgorithmName.SHA256,32))}";}
 public static bool Verify(string p,string hash){try{var parts=hash.Split('$');if(parts.Length!=4||parts[0]!="pbkdf2")return false;var actual=Rfc2898DeriveBytes.Pbkdf2(p,Convert.FromBase64String(parts[2]),int.Parse(parts[1]),HashAlgorithmName.SHA256,32);return CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(parts[3]));}catch{return false;}}
}
sealed class Db(IConfiguration config){
 public Task<List<Dictionary<string,object?>>> Query(string sql,params SqlParameter[] parameters)=>Execute(sql,CommandType.Text,parameters);
 public Task<List<Dictionary<string,object?>>> Procedure(string name,params SqlParameter[] parameters)=>Execute("dbo."+name,CommandType.StoredProcedure,parameters);
 private async Task<List<Dictionary<string,object?>>> Execute(string sql,CommandType type,SqlParameter[] parameters){
  await using var connection=new SqlConnection(config.GetConnectionString("SqlServer")??throw new InvalidOperationException("Configure ConnectionStrings__SqlServer"));await connection.OpenAsync();
  await using var command=new SqlCommand(sql,connection){CommandType=type,CommandTimeout=20};
  foreach(var p in parameters){p.Value??=DBNull.Value;command.Parameters.Add(p);}
  await using var reader=await command.ExecuteReaderAsync();var result=new List<Dictionary<string,object?>>();
  do{while(await reader.ReadAsync()){var row=new Dictionary<string,object?>();for(int i=0;i<reader.FieldCount;i++)row[JsonNamingPolicy.CamelCase.ConvertName(reader.GetName(i))]=reader.IsDBNull(i)?null:reader.GetValue(i);result.Add(row);}}while(await reader.NextResultAsync());
  return result;
 }
}



