using System;
using Logging.Default.Core;
using Logging.Default.Reservations;
using Logging.Default.Reservations.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Logging.Default.Controllers
{
    [Route("api/[controller]")]
    public class ReservationsController : Controller
    {
        private readonly ILogger<ReservationsController> logger;
        private readonly IReservationRepository repository;

        public ReservationsController(
            IReservationRepository repository,
            ILogger<ReservationsController> logger
        )
        {
            this.repository = repository;
            this.logger = logger;
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateReservation request)
        {
            using (logger.BeginScope("Create Reservation"))
            {
                logger.LogInformation("Initiating reservation creation for {seatId}", request?.SeatId);

                if (request?.SeatId == null || request?.SeatId == Guid.Empty)
                {
                    logger.LogWarning(LogEvents.InvalidRequest, "Invalid {SeatId}", request?.SeatId);

                    return BadRequest("Invalid SeatId");
                }

                var reservation = new Reservation {SeatId = request.SeatId};

                if (!repository.Create(reservation))
                {
                    logger.LogWarning(LogEvents.ConflictState, "Seat with {SeatId} is already reserved", reservation.SeatId);
                    return Conflict("Seat is already reserved");
                }

                logger.LogInformation("Successfully created reservation {ReservationId}", reservation.Id);
                return Created("api/Reservations", reservation.Id);
            }
        }

        [HttpPut]
        public IActionResult Update([FromBody] UpdateReservation request)
        {
            using (logger.BeginScope("Update Reservation"))
            {
                logger.LogInformation("Initiating reservation creation for {seatId}", request?.SeatId);

                if (request?.SeatId == null || request?.SeatId == Guid.Empty)
                {
                    logger.LogWarning(LogEvents.InvalidRequest, "Invalid {SeatId}", request?.SeatId);

                    return BadRequest("Invalid SeatId");
                }

                if (request?.ReservationId == null || request?.ReservationId == Guid.Empty)
                {
                    logger.LogWarning(LogEvents.InvalidRequest, "Invalid {ReservationId}", request?.ReservationId);

                    return BadRequest("Invalid ReservationId");
                }

                var reservation = repository.Find(request.ReservationId);

                reservation.SeatId = request.SeatId;

                if (!repository.Update(reservation))
                {
                    logger.LogWarning(LogEvents.EntityNotFound, "Seat with {SeatId} is already reserved", reservation.SeatId);

                    return Conflict("Seat is already reserved");
                }

                logger.LogInformation("Successfully updated reservation {ReservationId}", reservation.Id);
                return Created("api/Reservations", reservation.Id);
            }
        }
    }
}