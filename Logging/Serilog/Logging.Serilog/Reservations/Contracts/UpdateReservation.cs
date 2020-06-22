using System;

namespace Logging.Default.Reservations.Contracts
{
    public class UpdateReservation
    {
        public Guid ReservationId { get; set; }
        public Guid SeatId { get; set; }
    }
}