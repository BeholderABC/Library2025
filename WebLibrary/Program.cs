using Microsoft.AspNetCore.Authentication.Cookies;
using WebLibrary.Pages.Shared.Utils;

// >>> Scroll >>>
using Scroll.Database;
using Scroll.Services;
// <<< Scroll <<<

var builder = WebApplication.CreateBuilder(args);

// 1. 基础服务
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<NotificationService>();// 通知
builder.Services.AddScoped<NotificationStatusService>();//通知是否未读状态

// 2. Cookie 认证
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
       .AddCookie(options =>
       {
           options.LoginPath = "/Account/Login";
           options.LoginPath = "/Welcome";   
       });

// 3. 后台更新借阅状态
builder.Services.AddScoped<WebLibrary.Services.BorrowService>();
builder.Services.AddHostedService<WebLibrary.Services.BorrowStatusUpdateService>();

// 添加服务到容器
builder.Services.AddControllers();

// >>> Scroll >>>
// 注册数据库相关服务
builder.Services.AddScoped<DatabaseContext>();
builder.Services.AddScoped<IScrollDB, ScrollDB>();
builder.Services.AddScoped<ICurated, Curated>();
builder.Services.AddScoped<IPreference, Preference>();
builder.Services.AddScoped<ITop, Top>();

// 配置Oracle连接字符串
builder.Services.AddScoped(provider =>    // Singleton
{
    var configuration = provider.GetService<IConfiguration>();
    return new DatabaseContext(configuration);
});
// <<< Scroll <<<

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();   // 认证
app.UseAuthorization();    // 授权

app.UseSession();

app.MapRazorPages();
app.MapControllers();

// 4. 根路径 → 欢迎页
app.MapGet("/", () => Results.Redirect("/Welcome"));

app.Run();