using System;
using System.Globalization;
using System.Windows.Data;

namespace UserModule
{
    public class BookingIdConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string bookingId && !string.IsNullOrEmpty(bookingId))
            {
                // Display only the first 8-10 characters of the booking ID for better readability
                return bookingId.Length > 10 ? bookingId.Substring(0, 10) + "..." : bookingId;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
