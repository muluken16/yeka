using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Dapper;
using System.Data;

namespace CleaningManagmentSystem.Controllers
{
    [ApiController]
    [Route("api/transport")]
    public class TransportApiController : ControllerBase
    {
        private readonly string _cs;

        public TransportApiController(IConfiguration cfg)
            => _cs = cfg.GetConnectionString("DefaultConnection") ?? "";

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/transport/requests?userId=X&role=WeredaMahberat|Driver|DispatchOfficer
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("requests")]
        public IActionResult GetRequests([FromQuery] int? userId, [FromQuery] string? role, [FromQuery] string? status)
        {
            using var db = new MySqlConnection(_cs);
            var conditions = new List<string>();
            var param = new Dapper.DynamicParameters();

            if (role?.ToLower() == "weredamahberat" && userId.HasValue)
            {
                conditions.Add("tr.mahberat_user_id = @UserId");
                param.Add("UserId", userId.Value);
            }
            else if (role?.ToLower() == "driver" && userId.HasValue)
            {
                // Return ONLY requests assigned to this specific driver.
                // Primary: match on driver_user_id (set when dispatcher assigns).
                // Fallback: name-match via users table for older records without driver_user_id.
                conditions.Add(@"(
                    tr.driver_user_id = @UserId
                    OR (tr.driver_user_id IS NULL
                        AND tr.driver_name = (SELECT name FROM users WHERE id = @UserId LIMIT 1))
                )");
                param.Add("UserId", userId.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                conditions.Add("tr.status = @Status");
                param.Add("Status", status);
            }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            var sql = $@"
                SELECT tr.*,
                       COALESCE(tr.driver_name, d.full_name)    AS driver_name,
                       COALESCE(tr.vehicle_plate, v.plate_number) AS vehicle_plate
                FROM transport_requests tr
                LEFT JOIN drivers  d ON d.id = tr.driver_id
                LEFT JOIN vehicles v ON v.id = tr.vehicle_id
                {where}
                ORDER BY tr.created_at DESC";

            var rows = db.Query<dynamic>(sql, param).ToList();
            return Ok(rows);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/transport/requests/{id}
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("requests/{id:int}")]
        public IActionResult GetRequest(int id)
        {
            using var db = new MySqlConnection(_cs);

            var req = db.QueryFirstOrDefault<dynamic>(@"
                SELECT tr.*,
                       COALESCE(tr.driver_name, d.full_name)      AS driver_name,
                       COALESCE(tr.vehicle_plate, v.plate_number)  AS vehicle_plate
                FROM transport_requests tr
                LEFT JOIN drivers  d ON d.id = tr.driver_id
                LEFT JOIN vehicles v ON v.id = tr.vehicle_id
                WHERE tr.id = @Id", new { Id = id });

            if (req == null) return NotFound(new { success = false, message = "Not found" });

            var logs = db.Query<dynamic>(@"
                SELECT * FROM transport_request_logs
                WHERE transport_request_id = @Id
                ORDER BY created_at ASC", new { Id = id }).ToList();

            return Ok(new { request = req, logs });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/transport/stats?userId=X&role=WeredaMahberat
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("stats")]
        public IActionResult GetStats([FromQuery] int? userId, [FromQuery] string? role)
        {
            using var db = new MySqlConnection(_cs);
            string where = "";
            object param = new { UserId = userId ?? 0 };

            if (role?.ToLower() == "weredamahberat" && userId.HasValue)
                where = "AND mahberat_user_id = @UserId";

            var rows = db.Query<dynamic>(
                $"SELECT status, COUNT(*) AS cnt FROM transport_requests WHERE 1=1 {where} GROUP BY status",
                param).ToList();

            var map = rows.ToDictionary(
                r => (string)r.status,
                r => (int)r.cnt);

            int Get(params string[] keys) => keys.Sum(k => map.TryGetValue(k, out var v) ? v : 0);

            return Ok(new
            {
                pending         = Get("PendingDispatcher"),
                assigned        = Get("DriverAssigned"),
                accepted        = Get("DriverAccepted"),
                pickedUp        = Get("PickedUp"),
                mahberatApproved= Get("MahberatApprovedPickup"),
                receiptSubmitted= Get("ReceiptSubmitted"),
                receiptVerified = Get("ReceiptVerified", "MahberatVerified"),
                staffApproved   = Get("StaffApproved"),
                paid            = Get("Paid"),
                rejected        = Get("DispatcherRejected","DriverRejected","StaffRejected"),
                total           = map.Values.Sum()
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/transport/drivers/available
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("drivers/available")]
        public IActionResult GetAvailableDrivers()
        {
            using var db = new MySqlConnection(_cs);
            var drivers = db.Query<dynamic>(@"
                SELECT d.id,
                       d.full_name  AS name,
                       d.phone,
                       u.id         AS appUserId,
                       v.id         AS vehicleId,
                       v.plate_number AS vehiclePlate,
                       v.vehicle_type AS vehicleType
                FROM drivers d
                LEFT JOIN vehicles v ON v.driver_id = d.id AND v.status = 'Assigned'
                LEFT JOIN users u ON u.name = d.full_name AND u.role = 'driver' AND u.is_active = TRUE
                WHERE d.is_active = 1
                ORDER BY d.full_name").ToList();
            return Ok(drivers);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests   — Mahberat creates a new request
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests")]
        public IActionResult CreateRequest([FromBody] CreateRequestDto dto)
        {
            if (dto.UserId <= 0 || string.IsNullOrWhiteSpace(dto.PickupLocation) || string.IsNullOrWhiteSpace(dto.Destination))
                return BadRequest(new { success = false, message = "Missing required fields." });

            using var db = new MySqlConnection(_cs);

            string reqNum = $"TR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            db.Execute(@"
                INSERT INTO transport_requests
                    (request_number, mahberat_user_id, mahberat_user_name,
                     pickup_location, destination, passenger_item_details,
                     requested_date, requested_time, special_instructions,
                     status, created_at, updated_at)
                VALUES
                    (@Num, @Uid, @UName,
                     @Pickup, @Dest, @Details,
                     @Date, @Time, @Instructions,
                     'PendingDispatcher', NOW(), NOW())",
                new
                {
                    Num          = reqNum,
                    Uid          = dto.UserId,
                    UName        = dto.UserName ?? "",
                    Pickup       = dto.PickupLocation,
                    Dest         = dto.Destination,
                    Details      = dto.PassengerItemDetails ?? "",
                    Date         = dto.RequestedDate,
                    Time         = dto.RequestedTime ?? "",
                    Instructions = dto.SpecialInstructions ?? ""
                });

            var newId = db.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
            Log(db, newId, "", "PendingDispatcher", dto.UserId, dto.UserName ?? "", "WeredaMahberat", "Request created");

            return Ok(new { success = true, requestNumber = reqNum, id = newId });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/dispatcher-action
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/dispatcher-action")]
        public IActionResult DispatcherAction(int id, [FromBody] ActionDto dto)
        {
            using var db = new MySqlConnection(_cs);
            var req = db.QueryFirstOrDefault<dynamic>(
                "SELECT status FROM transport_requests WHERE id=@Id", new { Id = id });
            if (req == null) return NotFound(new { success = false, message = "Not found" });

            if ((string)req.status != "PendingDispatcher")
                return BadRequest(new { success = false, message = "Request is not pending." });

            if (dto.Action == "Approve")
            {
                if (dto.DriverId <= 0)
                    return BadRequest(new { success = false, message = "Driver is required." });

                // Get driver + vehicle info
                var driver = db.QueryFirstOrDefault<dynamic>(
                    "SELECT full_name, phone FROM drivers WHERE id=@Id", new { Id = dto.DriverId });
                string driverName = driver?.full_name ?? "";

                // Resolve the app user_id for this driver (so the mobile app can
                // filter requests by its own user.id via role=driver).
                int? driverUserId = db.ExecuteScalar<int?>(
                    "SELECT id FROM users WHERE name = @Name AND role = 'driver' AND is_active = TRUE LIMIT 1",
                    new { Name = driverName });

                string vehiclePlate = "";
                if (dto.VehicleId.HasValue && dto.VehicleId > 0)
                {
                    vehiclePlate = db.ExecuteScalar<string>(
                        "SELECT plate_number FROM vehicles WHERE id=@Id", new { Id = dto.VehicleId }) ?? "";
                }

                db.Execute(@"
                    UPDATE transport_requests
                    SET status='DriverAssigned', dispatcher_id=@Did, dispatcher_name=@DName,
                        dispatcher_notes=@Notes, dispatcher_action_at=NOW(),
                        driver_id=@DrvId, driver_name=@DrvName, driver_user_id=@DrvUserId,
                        vehicle_id=@VehId, vehicle_plate=@VehPlate,
                        updated_at=NOW()
                    WHERE id=@Id",
                    new { Did = dto.DispatcherId, DName = dto.DispatcherName, Notes = dto.Notes ?? "",
                          DrvId = dto.DriverId, DrvName = driverName, DrvUserId = driverUserId,
                          VehId = dto.VehicleId, VehPlate = vehiclePlate, Id = id });

                Log(db, id, "PendingDispatcher", "DriverAssigned", dto.DispatcherId, dto.DispatcherName ?? "", "DispatchOfficer", dto.Notes ?? "");
            }
            else
            {
                db.Execute(@"
                    UPDATE transport_requests
                    SET status='DispatcherRejected', dispatcher_id=@Did, dispatcher_name=@DName,
                        dispatcher_notes=@Notes, dispatcher_action_at=NOW(), updated_at=NOW()
                    WHERE id=@Id",
                    new { Did = dto.DispatcherId, DName = dto.DispatcherName, Notes = dto.Notes ?? "", Id = id });

                Log(db, id, "PendingDispatcher", "DispatcherRejected", dto.DispatcherId, dto.DispatcherName ?? "", "DispatchOfficer", dto.Notes ?? "");
            }

            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/driver-action
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/driver-action")]
        public IActionResult DriverAction(int id, [FromBody] ActionDto dto)
        {
            using var db = new MySqlConnection(_cs);
            var req = db.QueryFirstOrDefault<dynamic>(
                "SELECT status FROM transport_requests WHERE id=@Id", new { Id = id });
            if (req == null) return NotFound(new { success = false, message = "Not found" });

            string from = (string)req.status;
            string to   = dto.Action == "Accept" ? "DriverAccepted" : "DriverRejected";

            db.Execute(@"
                UPDATE transport_requests
                SET status=@To, driver_notes=@Notes, driver_action_at=NOW(), updated_at=NOW()
                WHERE id=@Id",
                new { To = to, Notes = dto.Notes ?? "", Id = id });

            Log(db, id, from, to, dto.DriverId > 0 ? dto.DriverId : dto.DispatcherId,
                dto.DriverName ?? dto.DispatcherName ?? "", "Driver", dto.Notes ?? "");

            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/driver-pickup
        //   Driver marks the item as picked up and submits receipt
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/driver-pickup")]
        public IActionResult DriverPickup(int id, [FromBody] DriverPickupDto dto)
        {
            using var db = new MySqlConnection(_cs);
            var req = db.QueryFirstOrDefault<dynamic>(
                "SELECT status FROM transport_requests WHERE id=@Id", new { Id = id });
            if (req == null) return NotFound(new { success = false, message = "Not found" });

            db.Execute(@"
                UPDATE transport_requests
                SET status='PickedUp',
                    pickup_confirmed_at=NOW(),
                    pickup_notes=@Notes,
                    receipt_photo_url=@Photo,
                    receipt_notes=@RNotes,
                    actual_kilogram=@Kg,
                    updated_at=NOW()
                WHERE id=@Id",
                new { Notes = dto.Notes ?? "", Photo = dto.ReceiptPhotoUrl ?? "",
                      RNotes = dto.ReceiptNotes ?? "", Kg = dto.ActualKilogram, Id = id });

            Log(db, id, "DriverAccepted", "PickedUp", dto.DriverId, dto.DriverName ?? "", "Driver", dto.Notes ?? "");
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/mahberat-pickup
        //   Mahberat confirms or rejects the pickup
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/mahberat-pickup")]
        public IActionResult MahberatPickup(int id, [FromBody] ActionDto dto)
        {
            using var db = new MySqlConnection(_cs);
            var req = db.QueryFirstOrDefault<dynamic>(
                "SELECT status FROM transport_requests WHERE id=@Id", new { Id = id });
            if (req == null) return NotFound(new { success = false, message = "Not found" });

            if (dto.Action == "Approve")
            {
                db.Execute(@"
                    UPDATE transport_requests
                    SET status='MahberatApprovedPickup',
                        mahberat_pickup_approved_at=NOW(),
                        mahberat_pickup_notes=@Notes,
                        updated_at=NOW()
                    WHERE id=@Id",
                    new { Notes = dto.Notes ?? "", Id = id });

                Log(db, id, "PickedUp", "MahberatApprovedPickup", dto.UserId, dto.UserName ?? "", "WeredaMahberat", dto.Notes ?? "");
            }
            else
            {
                // Reject: revert to DriverAccepted so driver can retry
                db.Execute(@"
                    UPDATE transport_requests
                    SET status='DriverAccepted',
                        mahberat_pickup_notes=@Notes,
                        updated_at=NOW()
                    WHERE id=@Id",
                    new { Notes = dto.Notes ?? "", Id = id });

                Log(db, id, "PickedUp", "DriverAccepted", dto.UserId, dto.UserName ?? "", "WeredaMahberat", $"Pickup rejected: {dto.Notes}");
            }

            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/receipt-submit
        //   Driver submits receipt after delivery
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/receipt-submit")]
        public IActionResult ReceiptSubmit(int id, [FromBody] ReceiptSubmitDto dto)
        {
            using var db = new MySqlConnection(_cs);
            var req = db.QueryFirstOrDefault<dynamic>(
                "SELECT status FROM transport_requests WHERE id=@Id", new { Id = id });
            if (req == null) return NotFound(new { success = false, message = "Not found" });

            db.Execute(@"
                UPDATE transport_requests
                SET status='ReceiptSubmitted',
                    receipt_photo_url=@Photo,
                    receipt_notes=@Notes,
                    actual_kilogram=@Kg,
                    receipt_submitted_at=NOW(),
                    updated_at=NOW()
                WHERE id=@Id",
                new { Photo = dto.ReceiptPhotoUrl ?? "", Notes = dto.Notes ?? "",
                      Kg = dto.ActualKilogram, Id = id });

            Log(db, id, "MahberatApprovedPickup", "ReceiptSubmitted", dto.DriverId, dto.DriverName ?? "", "Driver", dto.Notes ?? "");
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/mahberat-verify
        //   Mahberat level-1 verifies the receipt and forwards to Staff
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/mahberat-verify")]
        public IActionResult MahberatVerify(int id, [FromBody] ActionDto dto)
        {
            using var db = new MySqlConnection(_cs);
            var req = db.QueryFirstOrDefault<dynamic>(
                "SELECT status FROM transport_requests WHERE id=@Id", new { Id = id });
            if (req == null) return NotFound(new { success = false, message = "Not found" });

            string from = (string)req.status;

            if (dto.Action == "Verify")
            {
                db.Execute(@"
                    UPDATE transport_requests
                    SET status='MahberatVerified',
                        mahberat_verified_at=NOW(),
                        mahberat_verification_notes=@Notes,
                        level_1_mahberat_status='Approved',
                        level_1_date=NOW(),
                        updated_at=NOW()
                    WHERE id=@Id",
                    new { Notes = dto.Notes ?? "", Id = id });

                Log(db, id, from, "MahberatVerified", dto.UserId, dto.UserName ?? "", "WeredaMahberat", dto.Notes ?? "");
            }
            else // RequestCorrection
            {
                db.Execute(@"
                    UPDATE transport_requests
                    SET status='ReceiptSubmitted',
                        mahberat_verification_notes=@Notes,
                        updated_at=NOW()
                    WHERE id=@Id",
                    new { Notes = dto.Notes ?? "", Id = id });

                Log(db, id, from, "ReceiptSubmitted", dto.UserId, dto.UserName ?? "", "WeredaMahberat", $"Correction requested: {dto.Notes}");
            }

            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/staff-approve
        //   Staff final approval — sets cost and advances to StaffApproved
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/staff-approve")]
        public IActionResult StaffApprove(int id, [FromBody] StaffApproveDto dto)
        {
            using var db = new MySqlConnection(_cs);
            db.Execute(@"
                UPDATE transport_requests
                SET status='StaffApproved',
                    staff_id=@StaffId, staff_name=@StaffName,
                    transport_cost=@Cost,
                    staff_notes=@Notes,
                    staff_action_at=NOW(),
                    level_2_manager_status='Approved',
                    level_2_date=NOW(),
                    updated_at=NOW()
                WHERE id=@Id",
                new { StaffId = dto.StaffId, StaffName = dto.StaffName ?? "",
                      Cost = dto.TransportCost, Notes = dto.Notes ?? "", Id = id });

            Log(db, id, "MahberatVerified", "StaffApproved", dto.StaffId, dto.StaffName ?? "", "Staff", dto.Notes ?? "");
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/transport/requests/{id}/mark-paid
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("requests/{id:int}/mark-paid")]
        public IActionResult MarkPaid(int id, [FromBody] MarkPaidDto dto)
        {
            using var db = new MySqlConnection(_cs);
            db.Execute(@"
                UPDATE transport_requests
                SET status='Paid',
                    transport_cost=COALESCE(@Cost, transport_cost),
                    transaction_number=@TxNum,
                    paid_at=NOW(),
                    updated_at=NOW()
                WHERE id=@Id",
                new { Cost = dto.TransportCost, TxNum = dto.TransactionNumber ?? $"TXN-{DateTime.Now:yyyyMMddHHmmss}", Id = id });

            Log(db, id, "StaffApproved", "Paid", dto.StaffId, dto.StaffName ?? "", "Staff", dto.Notes ?? "");
            return Ok(new { success = true });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────
        private void Log(IDbConnection db, int requestId, string from, string to,
                         int actorId, string actorName, string actorRole, string notes)
        {
            try
            {
                db.Execute(@"
                    INSERT INTO transport_request_logs
                        (transport_request_id, from_status, to_status,
                         actor_user_id, actor_name, actor_role, notes, created_at)
                    VALUES
                        (@Rid, @From, @To, @Aid, @AName, @ARole, @Notes, NOW())",
                    new { Rid = requestId, From = from, To = to,
                          Aid = actorId, AName = actorName, ARole = actorRole, Notes = notes });
            }
            catch { /* non-critical */ }
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────
    public class CreateRequestDto
    {
        public int     UserId               { get; set; }
        public string? UserName             { get; set; }
        public string  PickupLocation       { get; set; } = "";
        public string  Destination          { get; set; } = "";
        public string? PassengerItemDetails { get; set; }
        public string? RequestedDate        { get; set; }
        public string? RequestedTime        { get; set; }
        public string? SpecialInstructions  { get; set; }
    }

    public class ActionDto
    {
        public string  Action         { get; set; } = "";
        public int     UserId         { get; set; }
        public string? UserName       { get; set; }
        public int     DispatcherId   { get; set; }
        public string? DispatcherName { get; set; }
        public int     DriverId       { get; set; }
        public string? DriverName     { get; set; }
        public int?    VehicleId      { get; set; }
        public string? Notes          { get; set; }
    }

    public class DriverPickupDto
    {
        public int     DriverId        { get; set; }
        public string? DriverName      { get; set; }
        public string? Notes           { get; set; }
        public string? ReceiptPhotoUrl { get; set; }
        public string? ReceiptNotes    { get; set; }
        public decimal? ActualKilogram { get; set; }
    }

    public class ReceiptSubmitDto
    {
        public int     DriverId        { get; set; }
        public string? DriverName      { get; set; }
        public string? ReceiptPhotoUrl { get; set; }
        public string? Notes           { get; set; }
        public decimal? ActualKilogram { get; set; }
    }

    public class StaffApproveDto
    {
        public int     StaffId        { get; set; }
        public string? StaffName      { get; set; }
        public decimal TransportCost  { get; set; }
        public string? Notes          { get; set; }
    }

    public class MarkPaidDto
    {
        public int     StaffId            { get; set; }
        public string? StaffName          { get; set; }
        public decimal? TransportCost     { get; set; }
        public string? TransactionNumber  { get; set; }
        public string? Notes              { get; set; }
    }
}
