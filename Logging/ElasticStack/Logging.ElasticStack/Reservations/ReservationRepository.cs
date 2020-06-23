using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Logging.Default.Reservations
{
    public interface IReservationRepository
    {
        bool Create(Reservation reservation);
        
        Reservation Find(Guid reservationId);
        
        bool Update(Reservation reservation);
    }
    
    /// <summary>
    /// Dummy repository using in memory dictionary as storage
    /// </summary>
    internal class ReservationRepository : IReservationRepository
    {
        private readonly IDictionary<Guid, Reservation> dbContext;
        private readonly ILogger<ReservationRepository> logger;

        public ReservationRepository(
            IDictionary<Guid, Reservation> dbContext,
            ILogger<ReservationRepository> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }
        
        public bool Create(Reservation reservation)
        {
            if (dbContext.Values.Any(r => r.SeatId == reservation.SeatId))
            {
                logger.LogWarning("Reservation for SeatId {SeatId} already exists", reservation.SeatId);
                return false;
            }

            var reservationId = Guid.NewGuid();

            using (logger.BeginScope("With ReservationId = {reservationId}", reservationId))
            {
                reservation.Id = reservationId;
                dbContext.Add(reservationId, reservation);
                
                logger.LogInformation("Reservation Added");
                return true;
            }
        }

        public Reservation Find(Guid reservationId)
        {
            using (logger.BeginScope("With ReservationId = {reservationId}", reservationId))
            {
                dbContext.TryGetValue(reservationId, out var reservation);

                if (reservation == null)
                {
                    logger.LogInformation("Reservation not found", reservationId);
                }

                return reservation;
            }
        }

        public bool Update(Reservation reservation)
        {
            using (logger.BeginScope("With ReservationId = {reservationId}", reservation.Id))
            {
                if (dbContext.Values.Any(r => r.SeatId == reservation.SeatId && r.Id != reservation.Id))
                {
                    logger.LogWarning("Reservation for SeatId {SeatId} already exists", reservation.SeatId);
                    return false;
                }
                
                dbContext[reservation.Id] = reservation;
                
                logger.LogInformation("Reservation Updated");
                return true;
            }
        }
    }
}