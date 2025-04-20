// עדכון ל-StopDetail.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace claudpro.Models
{
    public class StopDetail
    {
        public int StopNumber { get; set; }
        public int PassengerId { get; set; }
        public string PassengerName { get; set; }
        public double DistanceFromPrevious { get; set; }
        public double TimeFromPrevious { get; set; }
        public double CumulativeDistance { get; set; }
        public double CumulativeTime { get; set; }

        // הוספת תמיכה בזמני הגעה ויציאה כמחרוזות
        public string EstimatedArrivalTime { get; set; }
        public string EstimatedDepartureTime { get; set; }
    }
}