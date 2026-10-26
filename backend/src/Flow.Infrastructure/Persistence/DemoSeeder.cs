using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Domain.Entities;
using Flow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Persistence;

/// <summary>
/// datos de ejemplo para probar Flow en local (dos negocios de Manizales con su equipo y sus clientes y con citas
/// de las ultimas semanas y de las proximas)
/// solo se cargan si se activa la opcion DemoData de Seed y no se repiten (si el negocio ya existe se salta)
/// las fechas se calculan desde hoy en la zona horaria del negocio (la agenda nunca queda vacia ni vieja)
/// </summary>
public static class DemoSeeder
{
    public const string BarbershopEmail = "barberia@flow.demo";
    public const string BarbershopStaffEmail = "empleado.barberia@flow.demo";
    public const string SpaEmail = "spa@flow.demo";
    public const string SpaStaffEmail = "empleado.spa@flow.demo";

    private const int PastDays = 28;
    private const int FutureDays = 7;

    private sealed record ServiceSpec(string Name, string Description, int Minutes, decimal Price);

    // la persona con Account recibe una cuenta de empleado con ese correo
    private sealed record StaffSpec(string Name, string Color, int[] Services, DayOfWeek[] DaysOff, string? Account = null);

    private sealed record TenantSpec(
        string Name, string Slug, string Accent, string WhatsApp, string OwnerName, string OwnerEmail,
        ServiceSpec[] Services, StaffSpec[] Staff, string[] Customers, int Seed);

    private static readonly TenantSpec[] Tenants =
    [
        new("Barbería Don Mateo", "barberia-don-mateo", "#7c3aed", "573001110001", "Mateo Restrepo", BarbershopEmail,
        [
            new("Corte clásico", "Corte a tijera o máquina con acabado a navaja.", 30, 25000),
            new("Corte y barba", "Corte más perfilado y arreglo de barba.", 45, 38000),
            new("Afeitado con toalla caliente", "Afeitado tradicional con vapor y aceites.", 30, 22000),
            new("Perfilado de barba", "Definir contornos y rebajar largo.", 20, 15000),
            new("Tratamiento capilar", "Hidratación profunda y masaje de cuero cabelludo.", 40, 45000)
        ],
        [
            new("Mateo", "#7c3aed", [0, 1, 2, 3, 4], [DayOfWeek.Sunday]),
            new("Camilo", "#0ea5e9", [0, 1, 3], [DayOfWeek.Sunday, DayOfWeek.Wednesday], BarbershopStaffEmail),
            new("Julián", "#f59e0b", [0, 2, 3, 4], [DayOfWeek.Sunday, DayOfWeek.Monday])
        ],
        ["Andrés Gómez", "Sebastián Rojas", "Daniel Marín", "Juan Pablo Cardona", "Felipe Osorio", "Santiago Henao",
         "Nicolás Arias", "Esteban Ocampo", "Cristian Vélez", "David Zuluaga", "Tomás Giraldo", "Mauricio Londoño",
         "Alejandro Patiño", "Camilo Betancur", "Jorge Salazar", "Óscar Duque"], 11),

        new("Spa Montaña", "spa-montana", "#ec4899", "573001110002", "Valentina Aristizábal", SpaEmail,
        [
            new("Masaje relajante", "Masaje de cuerpo completo con aceites esenciales.", 60, 90000),
            new("Masaje descontracturante", "Trabajo profundo en espalda, cuello y hombros.", 60, 100000),
            new("Limpieza facial", "Limpieza profunda, exfoliación y mascarilla.", 45, 70000),
            new("Manicure spa", "Manicure con exfoliación e hidratación de manos.", 40, 35000),
            new("Pedicure spa", "Pedicure con baño de pies y masaje.", 50, 45000)
        ],
        [
            new("Valentina", "#ec4899", [0, 1, 2, 3, 4], [DayOfWeek.Sunday]),
            new("Laura", "#14b8a6", [0, 2, 3, 4], [DayOfWeek.Sunday, DayOfWeek.Tuesday], SpaStaffEmail),
            new("Andrés", "#6366f1", [0, 1], [DayOfWeek.Sunday, DayOfWeek.Saturday])
        ],
        ["Camila Ríos", "Daniela Castaño", "Paula Montoya", "Natalia Sánchez", "Juliana Herrera", "Manuela Echeverri",
         "Isabela Correa", "Mariana Quintero", "Lucía Franco", "Sara Valencia", "Carolina Mejía", "Andrea Toro",
         "Gabriela Ospina", "Laura Jaramillo", "Sofía Naranjo", "Diana Parra"], 23)
    ];

    public static async Task SeedAsync(IServiceProvider services, string password, CancellationToken ct = default)
    {
        foreach (var spec in Tenants)
        {
            // un scope por negocio (el contexto de negocio y el rastreo de cambios arrancan limpios)
            using var scope = services.CreateScope();
            await SeedTenantAsync(spec, password, scope.ServiceProvider, ct);
        }
    }

    private static async Task SeedTenantAsync(TenantSpec spec, string password, IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var tenantContext = sp.GetRequiredService<ITenantContext>();
        var users = sp.GetRequiredService<UserManager<AppUser>>();
        var links = sp.GetRequiredService<IManageLinks>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DemoSeeder));

        if (await db.Tenants.AnyAsync(t => t.Slug == spec.Slug, ct))
        {
            logger.LogInformation("Datos de ejemplo de {Slug}: ya existen, se omiten.", spec.Slug);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var tenant = new Tenant { Name = spec.Name, Slug = spec.Slug, AccentColor = spec.Accent, WhatsApp = spec.WhatsApp };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        tenantContext.Set(tenant.Id);

        await CreateUserAsync(users, spec.OwnerEmail, spec.OwnerName, tenant.Id, Roles.Owner, password);

        var services = spec.Services.Select(s => new BookableService
        {
            Name = s.Name, Description = s.Description, DurationMinutes = s.Minutes, Price = s.Price
        }).ToList();
        db.Services.AddRange(services);

        var staff = spec.Staff.Select(s => new StaffMember { Name = s.Name, Color = s.Color }).ToList();
        db.Staff.AddRange(staff);
        await db.SaveChangesAsync(ct);

        var staffServices = new List<StaffMemberService>();
        var hours = new List<WorkingHours>();
        for (var i = 0; i < staff.Count; i++)
        {
            staffServices.AddRange(spec.Staff[i].Services.Select(index => new StaffMemberService
            {
                StaffMemberId = staff[i].Id, ServiceId = services[index].Id
            }));
            hours.AddRange(WeeklyHours(staff[i].Id, spec.Staff[i].DaysOff));

            if (spec.Staff[i].Account is { } accountEmail)
            {
                var account = await CreateUserAsync(users, accountEmail, $"{spec.Staff[i].Name} (empleado)", tenant.Id, Roles.Staff, password);
                staff[i].UserId = account.Id;
            }
        }
        db.StaffServices.AddRange(staffServices);
        db.WorkingHours.AddRange(hours);

        var zone = BusinessTime.Zone(tenant.TimeZone);
        var today = BusinessTime.Today(zone, clock);
        db.TimeOff.AddRange(
            new TimeOff
            {
                StartsAtUtc = BusinessTime.StartOfDayUtc(today.AddDays(FutureDays + 2), zone),
                EndsAtUtc = BusinessTime.StartOfDayUtc(today.AddDays(FutureDays + 3), zone),
                Reason = "Festivo"
            },
            new TimeOff
            {
                StaffMemberId = staff[1].Id,
                StartsAtUtc = BusinessTime.StartOfDayUtc(today.AddDays(FutureDays + 4), zone),
                EndsAtUtc = BusinessTime.StartOfDayUtc(today.AddDays(FutureDays + 6), zone),
                Reason = "Vacaciones"
            });
        await db.SaveChangesAsync(ct);

        var customers = spec.Customers.Select((name, i) => new Customer
        {
            Name = name,
            Phone = $"5730000{spec.Seed:00}{i + 1:00}"
        }).ToList();
        customers[0].Notes = "Prefiere las citas a primera hora.";
        customers[3].Notes = "Alergia a algunos aceites, preguntar antes de aplicar productos.";

        var random = new Random(spec.Seed);
        var appointments = BuildAppointments(spec, tenant, zone, today, clock.GetUtcNow().UtcDateTime, random,
            staff, services, hours, customers, links);

        // consentimiento de privacidad para los clientes que reservaron en linea (los demas los registro el negocio)
        foreach (var customer in customers)
        {
            var first = appointments.Where(a => a.CustomerId == customer.Id).MinBy(a => a.StartsAtUtc);
            if (first?.Source == AppointmentSource.Online) customer.PrivacyConsentAt = first.StartsAtUtc.AddDays(-1);
        }

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);
        db.Appointments.AddRange(appointments);
        await db.SaveChangesAsync(ct);

        await BackdateAsync(db, customers, appointments, clock.GetUtcNow().UtcDateTime, ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Datos de ejemplo de {Name}: {Appointments} citas, {Customers} clientes. Dueño: {Owner}",
            spec.Name, appointments.Count, customers.Count, spec.OwnerEmail);
    }

    private static async Task<AppUser> CreateUserAsync(
        UserManager<AppUser> users, string email, string fullName, Guid tenantId, string role, string password)
    {
        var user = new AppUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = fullName,
            TenantId = tenantId, CreatedAt = DateTime.UtcNow
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                $"No se pudo crear la cuenta de ejemplo {email}: {string.Join(" ", created.Errors.Select(e => e.Description))}");

        await users.AddToRoleAsync(user, role);
        return user;
    }

    // lunes a viernes por la manana y la tarde y sabados hasta el medio dia
    private static IEnumerable<WorkingHours> WeeklyHours(Guid staffId, DayOfWeek[] daysOff)
    {
        foreach (var day in Enum.GetValues<DayOfWeek>().Where(d => !daysOff.Contains(d)))
        {
            if (day == DayOfWeek.Saturday)
            {
                yield return new WorkingHours { StaffMemberId = staffId, DayOfWeek = day, Start = new(9, 0), End = new(14, 0) };
                continue;
            }

            yield return new WorkingHours { StaffMemberId = staffId, DayOfWeek = day, Start = new(9, 0), End = new(12, 30) };
            yield return new WorkingHours { StaffMemberId = staffId, DayOfWeek = day, Start = new(14, 0), End = new(19, 0) };
        }
    }

    private static List<Appointment> BuildAppointments(
        TenantSpec spec, Tenant tenant, TimeZoneInfo zone, DateOnly today, DateTime nowUtc, Random random,
        List<StaffMember> staff, List<BookableService> services, List<WorkingHours> hours, List<Customer> customers,
        IManageLinks links)
    {
        var result = new List<Appointment>();

        for (var offset = -PastDays; offset <= FutureDays; offset++)
        {
            var date = today.AddDays(offset);
            // los dias proximos van mas vacios que los pasados (todavia se pueden reservar)
            var fill = offset > 0 ? 0.55 : 0.72;

            for (var s = 0; s < staff.Count; s++)
            {
                var offered = spec.Staff[s].Services.Select(index => services[index]).ToList();
                var blocks = hours
                    .Where(h => h.StaffMemberId == staff[s].Id && h.DayOfWeek == date.DayOfWeek)
                    .OrderBy(h => h.Start);

                foreach (var block in blocks)
                {
                    var cursor = block.Start;
                    while (true)
                    {
                        if (random.NextDouble() < 0.25) cursor = cursor.AddMinutes(15 * random.Next(1, 5));

                        var service = offered[random.Next(offered.Count)];
                        var end = cursor.AddMinutes(service.DurationMinutes);
                        if (end > block.End || end < cursor) break;

                        if (random.NextDouble() < fill)
                        {
                            var appointment = NewAppointment(
                                tenant, zone, date, cursor, end, service, staff[s],
                                customers[random.Next(customers.Count)], nowUtc, random, links);
                            if (appointment is not null) result.Add(appointment);
                        }

                        cursor = end;
                    }
                }
            }
        }

        return result;
    }

    private static Appointment? NewAppointment(
        Tenant tenant, TimeZoneInfo zone, DateOnly date, TimeOnly start, TimeOnly end, BookableService service,
        StaffMember staff, Customer customer, DateTime nowUtc, Random random, IManageLinks links)
    {
        if (!BusinessTime.TryToUtc(date.ToDateTime(start), zone, out var startUtc)) return null;
        if (!BusinessTime.TryToUtc(date.ToDateTime(end), zone, out var endUtc)) return null;

        var roll = random.NextDouble();
        var status = endUtc <= nowUtc
            ? roll < 0.80 ? AppointmentStatus.Completed : roll < 0.88 ? AppointmentStatus.NoShow : AppointmentStatus.Cancelled
            : roll < 0.92 ? AppointmentStatus.Confirmed : AppointmentStatus.Cancelled;

        var appointment = new Appointment
        {
            TenantId = tenant.Id,
            CustomerId = customer.Id,
            ServiceId = service.Id,
            StaffMemberId = staff.Id,
            StartsAtUtc = startUtc,
            EndsAtUtc = endUtc,
            Status = status,
            Source = random.NextDouble() < 0.6 ? AppointmentSource.Online : AppointmentSource.Panel,
            Price = service.Price
        };
        appointment.ManageTokenHash = SecureTokens.Hash(links.TokenFor(appointment.Id));
        if (status == AppointmentStatus.Cancelled)
            appointment.CancelledAt = new[] { nowUtc, startUtc.AddHours(-random.Next(2, 48)) }.Min();
        return appointment;
    }

    // el interceptor pone CreatedAt en la fecha actual asi que se retroceden a mano para que las metricas
    // (clientes nuevos por periodo) y el historial se vean como los de un negocio con semanas de uso
    private static async Task BackdateAsync(
        AppDbContext db, List<Customer> customers, List<Appointment> appointments, DateTime nowUtc, CancellationToken ct)
    {
        foreach (var customer in customers)
        {
            var first = appointments.Where(a => a.CustomerId == customer.Id).Select(a => a.StartsAtUtc).DefaultIfEmpty(nowUtc).Min();
            var createdAt = first.AddDays(-1) < nowUtc ? first.AddDays(-1) : nowUtc;
            var id = customer.Id;
            await db.Customers.Where(c => c.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, createdAt).SetProperty(c => c.UpdatedAt, createdAt), ct);
        }

        await db.Appointments.Where(a => a.StartsAtUtc < nowUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.CreatedAt, a => a.StartsAtUtc.AddDays(-2))
                .SetProperty(a => a.UpdatedAt, a => a.EndsAtUtc), ct);
    }
}
