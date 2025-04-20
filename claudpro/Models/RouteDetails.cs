// עדכון ל-RouteDetails.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace claudpro.Models
{
    public class RouteDetails
    {
        public int VehicleId { get; set; }
        public double TotalDistance { get; set; }
        public double TotalTime { get; set; }
        public List<StopDetail> StopDetails { get; set; } = new List<StopDetail>();

        // הוספת שדה לזמן היציאה הראשוני
        public string DepartureTime { get; set; }
    }
}