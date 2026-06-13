using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCredito.Data;
using PlataformaCredito.Models;
using PlataformaCredito.ViewModels;

namespace PlataformaCredito.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public SolicitudesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // GET: /Solicitudes
    public async Task<IActionResult> Index(SolicitudFiltroViewModel filtro)
    {
        // Validaciones server-side de filtros
        if (filtro.MontoMin.HasValue && filtro.MontoMin < 0)
        {
            filtro.ErrorFiltro = "El monto mínimo no puede ser negativo.";
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        if (filtro.MontoMax.HasValue && filtro.MontoMax < 0)
        {
            filtro.ErrorFiltro = "El monto máximo no puede ser negativo.";
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        if (filtro.FechaInicio.HasValue && filtro.FechaFin.HasValue
            && filtro.FechaInicio > filtro.FechaFin)
        {
            filtro.ErrorFiltro = "La fecha de inicio no puede ser mayor a la fecha fin.";
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null)
        {
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        var query = _db.SolicitudesCredito
            .Where(s => s.ClienteId == cliente.Id)
            .AsQueryable();

        if (filtro.Estado.HasValue)
            query = query.Where(s => s.Estado == filtro.Estado);

        if (filtro.MontoMin.HasValue)
            query = query.Where(s => s.MontoSolicitado >= filtro.MontoMin);

        if (filtro.MontoMax.HasValue)
            query = query.Where(s => s.MontoSolicitado <= filtro.MontoMax);

        if (filtro.FechaInicio.HasValue)
            query = query.Where(s => s.FechaSolicitud >= filtro.FechaInicio.Value);

        if (filtro.FechaFin.HasValue)
            query = query.Where(s => s.FechaSolicitud <= filtro.FechaFin.Value.AddDays(1));

        filtro.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        return View(filtro);
    }

    // GET: /Solicitudes/Crear
    public async Task<IActionResult> Crear()
    {
        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null || !cliente.Activo)
        {
            TempData["Error"] = "No tienes un perfil de cliente activo.";
            return RedirectToAction(nameof(Index));
        }

        return View(new CrearSolicitudViewModel());
    }

    // POST: /Solicitudes/Crear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearSolicitudViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes
            .Include(c => c.Solicitudes)
            .FirstOrDefaultAsync(c => c.UsuarioId == userId);

        // Validación: cliente activo
        if (cliente == null || !cliente.Activo)
        {
            ModelState.AddModelError("", "No tienes un perfil de cliente activo.");
            return View(vm);
        }

        // Validación: no más de una solicitud Pendiente
        var tienePendiente = cliente.Solicitudes
            .Any(s => s.Estado == EstadoSolicitud.Pendiente);

        if (tienePendiente)
        {
            ModelState.AddModelError("", "Ya tienes una solicitud en estado Pendiente. Espera a que sea procesada.");
            return View(vm);
        }

        // Validación: monto no supera 10x ingresos
        if (vm.MontoSolicitado > cliente.IngresosMensuales * 10)
        {
            ModelState.AddModelError("MontoSolicitado",
                $"El monto no puede superar 10 veces tus ingresos mensuales (máx. {(cliente.IngresosMensuales * 10):C}).");
            return View(vm);
        }

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = vm.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };

        _db.SolicitudesCredito.Add(solicitud);
        await _db.SaveChangesAsync();

        TempData["Exito"] = $"Solicitud por {vm.MontoSolicitado:C} registrada exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Solicitudes/Detalle/5
    public async Task<IActionResult> Detalle(int id)
    {
        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null) return NotFound();

        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == cliente.Id);

        if (solicitud == null) return NotFound();

        return View(solicitud);
    }
}
