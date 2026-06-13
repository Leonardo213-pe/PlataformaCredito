using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCredito.Data;
using PlataformaCredito.Models;
using PlataformaCredito.Services;

namespace PlataformaCredito.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SolicitudCacheService _cache;
    private readonly UserManager<ApplicationUser> _userManager;

    public AnalistaController(ApplicationDbContext db, SolicitudCacheService cache, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _cache = cache;
        _userManager = userManager;
    }

    // GET: /Analista
    public async Task<IActionResult> Index()
    {
        var pendientes = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
                .ThenInclude(c => c!.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(pendientes);
    }

    // POST: /Analista/Aprobar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["Error"] = "Solicitud no encontrada.";
            return RedirectToAction(nameof(Index));
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "Solo se pueden procesar solicitudes en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        if (solicitud.MontoSolicitado > solicitud.Cliente!.IngresosMensuales * 5)
        {
            TempData["Error"] = $"No se puede aprobar: el monto excede 5 veces los ingresos del cliente (máx. {(solicitud.Cliente.IngresosMensuales * 5):C}).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        await _db.SaveChangesAsync();

        // Invalidar caché del cliente
        await _cache.InvalidarAsync(solicitud.Cliente.UsuarioId);

        TempData["Exito"] = $"Solicitud #{id} aprobada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Analista/Rechazar/5
    public async Task<IActionResult> Rechazar(int id)
    {
        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
                .ThenInclude(c => c!.Usuario)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "Solo se pueden procesar solicitudes en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        return View(solicitud);
    }

    // POST: /Analista/Rechazar/5
    [HttpPost, ActionName("Rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RechazarConfirmado(int id, string motivoRechazo)
    {
        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            ModelState.AddModelError("", "El motivo de rechazo es obligatorio.");
            var s = await _db.SolicitudesCredito
                .Include(s => s.Cliente).ThenInclude(c => c!.Usuario)
                .FirstOrDefaultAsync(s => s.Id == id);
            return View(s);
        }

        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "Solo se pueden procesar solicitudes en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo.Trim();
        await _db.SaveChangesAsync();

        // Invalidar caché del cliente
        await _cache.InvalidarAsync(solicitud.Cliente!.UsuarioId);

        TempData["Exito"] = $"Solicitud #{id} rechazada.";
        return RedirectToAction(nameof(Index));
    }
}
