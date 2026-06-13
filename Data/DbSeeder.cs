using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCredito.Models;

namespace PlataformaCredito.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        // Roles
        if (!await roleManager.RoleExistsAsync("Analista"))
            await roleManager.CreateAsync(new IdentityRole("Analista"));
        if (!await roleManager.RoleExistsAsync("Cliente"))
            await roleManager.CreateAsync(new IdentityRole("Cliente"));

        // Usuario analista
        if (await userManager.FindByEmailAsync("analista@demo.com") == null)
        {
            var analista = new ApplicationUser { UserName = "analista@demo.com", Email = "analista@demo.com", EmailConfirmed = true };
            await userManager.CreateAsync(analista, "Analista123!");
            await userManager.AddToRoleAsync(analista, "Analista");
        }

        // Cliente 1 — solicitud Pendiente
        if (await userManager.FindByEmailAsync("cliente1@demo.com") == null)
        {
            var u1 = new ApplicationUser { UserName = "cliente1@demo.com", Email = "cliente1@demo.com", EmailConfirmed = true };
            await userManager.CreateAsync(u1, "Cliente123!");
            await userManager.AddToRoleAsync(u1, "Cliente");

            var c1 = new Cliente { UsuarioId = u1.Id, IngresosMensuales = 5000, Activo = true };
            context.Clientes.Add(c1);
            await context.SaveChangesAsync();

            context.SolicitudesCredito.Add(new SolicitudCredito
            {
                ClienteId = c1.Id,
                MontoSolicitado = 10000,
                FechaSolicitud = DateTime.UtcNow.AddDays(-3),
                Estado = EstadoSolicitud.Pendiente
            });
            await context.SaveChangesAsync();
        }

        // Cliente 2 — solicitud Aprobada
        if (await userManager.FindByEmailAsync("cliente2@demo.com") == null)
        {
            var u2 = new ApplicationUser { UserName = "cliente2@demo.com", Email = "cliente2@demo.com", EmailConfirmed = true };
            await userManager.CreateAsync(u2, "Cliente123!");
            await userManager.AddToRoleAsync(u2, "Cliente");

            var c2 = new Cliente { UsuarioId = u2.Id, IngresosMensuales = 8000, Activo = true };
            context.Clientes.Add(c2);
            await context.SaveChangesAsync();

            context.SolicitudesCredito.Add(new SolicitudCredito
            {
                ClienteId = c2.Id,
                MontoSolicitado = 15000,
                FechaSolicitud = DateTime.UtcNow.AddDays(-10),
                Estado = EstadoSolicitud.Aprobado
            });
            await context.SaveChangesAsync();
        }
    }
}
