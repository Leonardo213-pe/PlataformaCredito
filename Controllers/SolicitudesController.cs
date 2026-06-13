using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCredito.Data;
using PlataformaCredito.Models;
using PlataformaCredito.Services;
using PlataformaCredito.ViewModels;

namespace PlataformaCredito.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SolicitudCacheService _cache;

    private const string SessionKeyId     = "UltimaSolicitudId";
    private const string SessionKeyMonto  = "UltimaSolicitudMonto";

    public SolicitudesController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        SolicitudCacheService cache)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
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

        var userId = _userManager.GetUserId(User)!;
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);
        if (cliente == null) { filtro.Solicitudes = new(); return View(filtro); }

        // Si no hay filtros activos, intenta usar caché
        bool hayFiltros = filtro.Estado.HasValue || filtro.MontoMin.HasValue
            || filtro.MontoMax.HasValue || filtro.FechaInicio.HasValue || filtro.FechaFin.HasValue;

        List<SolicitudCredito> todas;
        if (!hayFiltros)
        {
            todas = await _cache.ObtenerAsync(userId) ?? await CargarYCachearAsync(userId, cliente.Id);
        }
        else
        {
            todas = await ConsultarConFiltrosAsync(cliente.Id, filtro);
        }

        filtro.Solicitudes = hayFiltros
            ? todas
            : todas; // ya filtrados en ConsultarConFiltrosAsync

        if (hayFiltros)
            filtro.Solicitudes = await ConsultarConFiltrosAsync(cliente.Id, filtro);
        else
            filtro.Solicitudes = todas;

        return View(filtro);
    }

    private async Task<List<SolicitudCredito>> CargarYCachearAsync(string userId, int clienteId)
    {
        var lista = await _db.SolicitudesCredito
            .Where(s => s.ClienteId == clienteId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        await _cache.GuardarAsync(userId, lista);
        return lista;
    }

    private async Task<List<SolicitudCredito>> ConsultarConFiltrosAsync(int clienteId, SolicitudFiltroViewModel filtro)
    {
        var query = _db.SolicitudesCredito
            .Where(s => s.ClienteId == clienteId)
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

        return await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();
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

        // Guardar última solicitud visitada en sesión
        HttpContext.Session.SetInt32(SessionKeyId, solicitud.Id);
        HttpContext.Session.SetString(SessionKeyMonto, solicitud.MontoSolicitado.ToString("C"));

        return View(solicitud);
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
        if (!ModelState.IsValid) return View(vm);

        var userId = _userManager.GetUserId(User)!;
        var cliente = await _db.Clientes
            .Include(c => c.Solicitudes)
            .FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null || !cliente.Activo)
        {
            ModelState.AddModelError("", "No tienes un perfil de cliente activo.");
            return View(vm);
        }

        if (cliente.Solicitudes.Any(s => s.Estado == EstadoSolicitud.Pendiente))
        {
            ModelState.AddModelError("", "Ya tienes una solicitud en estado Pendiente. Espera a que sea procesada.");
            return View(vm);
        }

        if (vm.MontoSolicitado > cliente.IngresosMensuales * 10)
        {
            ModelState.AddModelError("MontoSolicitado",
                $"El monto no puede superar 10 veces tus ingresos mensuales (máx. {(cliente.IngresosMensuales * 10):C}).");
            return View(vm);
        }

        _db.SolicitudesCredito.Add(new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = vm.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        });
        await _db.SaveChangesAsync();

        // Invalidar caché al crear nueva solicitud
        await _cache.InvalidarAsync(userId);

        TempData["Exito"] = $"Solicitud por {vm.MontoSolicitado:C} registrada exitosamente.";
        return RedirectToAction(nameof(Index));
    }
}
