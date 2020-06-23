using System;

namespace Logging.Default.Reservations
{
    public class Reservation
    {
        public Guid Id { get; set; }
        
        public Guid SeatId { get; set; }
    }
}