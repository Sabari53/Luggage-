using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UserModule.Views
{
    public partial class Luggage : UserControl
    {
        private static readonly Regex LettersRegex = new Regex("^[a-zA-Z ]+$");
        private static readonly Regex NumbersRegex = new Regex("^[0-9]+$");

        private System.Collections.Generic.List<string> allLockers = new System.Collections.Generic.List<string>();
        private System.Collections.Generic.List<string> selectedLockersList = new System.Collections.Generic.List<string>();

        public ObservableCollection<LuggageItem> Items { get; } = new ObservableCollection<LuggageItem>();

        public ObservableCollection<string> LuggageTypes { get; } = new ObservableCollection<string>
        {
            "Bag", "Suitcase", "Parcel", "Box", "Envelope", "Backpack", "Briefcase", "Duffel Bag",
            "Travel Bag", "Laptop Bag", "Handbag", "Tote Bag", "Messenger Bag", "Garment Bag",
            "Trolley Bag", "Sports Bag", "Camera Bag", "Cosmetic Bag", "Jewelry Box", "Document Folder"
        };

        private readonly System.Collections.Generic.Dictionary<string, decimal> priceMap = new()
        {
            { "Bag", 50m }, { "Suitcase", 120m }, { "Parcel", 80m }, { "Box", 200m }, { "Envelope", 20m },
            { "Backpack", 70m }, { "Briefcase", 150m }, { "Duffel Bag", 90m }, { "Travel Bag", 110m },
            { "Laptop Bag", 80m }, { "Handbag", 40m }, { "Tote Bag", 45m }, { "Messenger Bag", 65m },
            { "Garment Bag", 130m }, { "Trolley Bag", 180m }, { "Sports Bag", 75m }, { "Camera Bag", 95m },
            { "Cosmetic Bag", 30m }, { "Jewelry Box", 160m }, { "Document Folder", 25m }
        };

        public Luggage()
        {
            InitializeComponent();
            DataContext = this;
            InitializeData();
            Items.CollectionChanged += Items_CollectionChanged;

            // Add event handler to prevent row selection issues
            LuggageGrid.SelectionChanged += LuggageGrid_SelectionChanged;

            // Initialize date and time automatically
            UpdateDateTime(null, EventArgs.Empty);
        }

        // Handle selection changed to prevent row selection conflicts
        private void LuggageGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is DataGrid dg)
                {
                    // Clear any row selections to prevent conflicts
                    if (dg.SelectedItems.Count > 0)
                    {
                        dg.SelectedItems.Clear();
                    }

                    // Ensure we only work with cell selection
                    if (dg.SelectedCells.Count > 1)
                    {
                        // Keep only the last selected cell
                        var lastCell = dg.SelectedCells.LastOrDefault();
                        dg.SelectedCells.Clear();
                        if (lastCell.IsValid)
                        {
                            dg.SelectedCells.Add(lastCell);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageGrid_SelectionChanged: {ex.Message}");
            }
        }

        private void UpdateDateTime(object? sender, EventArgs e)
        {
            txtBookingDate.Text = DateTime.Now.ToString("MM/dd/yyyy");
            txtBookingTime.Text = DateTime.Now.ToString("HH:mm");
        }

        private void InitializeData()
        {
            try
            {
                Items.Clear();

                // Add initial items with proper price calculation
                var bagItem = new LuggageItem { LuggageType = "Bag", Quantity = 2 };
                if (priceMap.TryGetValue("Bag", out var bagPrice))
                {
                    bagItem.UpdateValues(bagPrice, bagPrice * 2, true);
                }
                Items.Add(bagItem);

                var suitcaseItem = new LuggageItem { LuggageType = "Suitcase", Quantity = 1 };
                if (priceMap.TryGetValue("Suitcase", out var suitcasePrice))
                {
                    suitcaseItem.UpdateValues(suitcasePrice, suitcasePrice * 1, true);
                }
                Items.Add(suitcaseItem);

                // Add empty rows (minimum 3 rows)
                while (Items.Count < 3)
                    Items.Add(new LuggageItem());

                RefreshItemNumbers();

                // Set initial DataGrid height
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateDataGridHeight();
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                System.Diagnostics.Debug.WriteLine("InitializeData completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in InitializeData: {ex.Message}");
            }
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (var it in e.NewItems.OfType<LuggageItem>())
                    it.PropertyChanged += Item_PropertyChanged;

            if (e.OldItems != null)
                foreach (var it in e.OldItems.OfType<LuggageItem>())
                    it.PropertyChanged -= Item_PropertyChanged;

            RefreshItemNumbers();

            // Update DataGrid height when items are added/removed
            UpdateDataGridHeight();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not LuggageItem item) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"Property changed: {e.PropertyName} for item with type: {item.LuggageType}, quantity: {item.Quantity}");

                // Handle luggage type change - set price first
                if (e.PropertyName == nameof(LuggageItem.LuggageType))
                {
                    if (!string.IsNullOrEmpty(item.LuggageType) && priceMap.TryGetValue(item.LuggageType, out var price))
                    {
                        System.Diagnostics.Debug.WriteLine($"Setting price for {item.LuggageType}: {price}");
                        // Temporarily disconnect to avoid recursive calls
                        item.PropertyChanged -= Item_PropertyChanged;
                        item.Price = price;
                        item.TotalAmount = item.Price * item.Quantity;
                        item.PropertyChanged += Item_PropertyChanged;
                        System.Diagnostics.Debug.WriteLine($"Price set to: {item.Price}, Total: {item.TotalAmount}");
                    }
                    else
                    {
                        item.PropertyChanged -= Item_PropertyChanged;
                        item.Price = 0m;
                        item.TotalAmount = 0m;
                        item.PropertyChanged += Item_PropertyChanged;
                    }
                }
                // Handle quantity change - recalculate total
                else if (e.PropertyName == nameof(LuggageItem.Quantity))
                {
                    System.Diagnostics.Debug.WriteLine($"Quantity changed to: {item.Quantity}, Price: {item.Price}");
                    // Temporarily disconnect to avoid recursive calls
                    item.PropertyChanged -= Item_PropertyChanged;
                    if (item.Quantity < 0) item.Quantity = 0;
                    item.TotalAmount = item.Price * item.Quantity;
                    item.PropertyChanged += Item_PropertyChanged;
                    System.Diagnostics.Debug.WriteLine($"Total amount calculated: {item.TotalAmount}");
                }
                // Handle price change - recalculate total
                else if (e.PropertyName == nameof(LuggageItem.Price))
                {
                    System.Diagnostics.Debug.WriteLine($"Price changed to: {item.Price}, Quantity: {item.Quantity}");
                    // Temporarily disconnect to avoid recursive calls
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.TotalAmount = item.Price * item.Quantity;
                    item.PropertyChanged += Item_PropertyChanged;
                    System.Diagnostics.Debug.WriteLine($"Total amount recalculated: {item.TotalAmount}");
                }

                // FIXED: Only auto-add new row when BOTH luggage type AND quantity are filled
                // This prevents automatic row generation while typing
                if ((e.PropertyName == nameof(LuggageItem.LuggageType) || e.PropertyName == nameof(LuggageItem.Quantity)) &&
                    IsRowCompletelyFilled(item))
                {
                    var itemIndex = Items.IndexOf(item);
                    // Only add if this is the last row and we haven't reached the limit
                    if (itemIndex == Items.Count - 1 && Items.Count < 50)
                    {
                        System.Diagnostics.Debug.WriteLine("Adding new row - current row is completely filled");
                        Items.Add(new LuggageItem());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Item_PropertyChanged: {ex.Message}");
                MessageBox.Show($"Error updating item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Helper method to check if a row is completely filled
        private bool IsRowCompletelyFilled(LuggageItem item)
        {
            return !string.IsNullOrWhiteSpace(item.LuggageType) && item.Quantity > 0;
        }

        private void RefreshItemNumbers()
        {
            try
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i];
                    if (item != null)
                    {
                        item.SNo = i + 1;

                        // Recalculate total amount for each item
                        if (item.Quantity >= 0 && item.Price >= 0)
                        {
                            item.TotalAmount = item.Price * item.Quantity;
                        }
                        else
                        {
                            item.TotalAmount = 0m;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RefreshItemNumbers: {ex.Message}");
            }
        }

        // Event Handlers
        private void UserControl_Loaded(object sender, RoutedEventArgs e) 
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== UserControl_Loaded Started ===");

                // Populate all available lockers (1-60) - full list for searching
                if (allLockers.Count == 0)
                {
                    for (int i = 1; i <= 60; i++)
                    {
                        allLockers.Add($"Locker {i}");
                    }
                    System.Diagnostics.Debug.WriteLine($"✓ Created allLockers list with {allLockers.Count} items");
                }
                
                // Initialize the ComboBox with all lockers
                if (cmbLockerNumber != null)
                {
                    cmbLockerNumber.Items.Clear();
                    foreach (var locker in allLockers)
                    {
                        cmbLockerNumber.Items.Add(locker);
                    }
                    System.Diagnostics.Debug.WriteLine($"✓ cmbLockerNumber populated with {cmbLockerNumber.Items.Count} items");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✗ WARNING: cmbLockerNumber is NULL");
                }

                System.Diagnostics.Debug.WriteLine("=== UserControl_Loaded Completed ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in UserControl_Loaded: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CustomerName_TextChanged(object sender, TextChangedEventArgs e) { }

        private void UppercaseOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z ]+$");
        }

        private void UppercaseOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = (e.DataObject.GetData(DataFormats.Text) as string) ?? string.Empty;
                if (!LettersRegex.IsMatch(text))
                    e.CancelCommand();
            }
            else
                e.CancelCommand();
        }

        private void NumbersOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void NumbersOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var text = (e.DataObject.GetData(DataFormats.Text) as string) ?? string.Empty;
                if (!NumbersRegex.IsMatch(text))
                    e.CancelCommand();
            }
            else
                e.CancelCommand();
        }

        private void Control_PreviewKeyDown(object sender, KeyEventArgs e) { }

        // Enhanced GenerateBill_Click with comprehensive validation
        private async void GenerateBill_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear all error messages first
                errCustomer1.Visibility = Visibility.Collapsed;
                errPhone1.Visibility = Visibility.Collapsed;
                errIdType1.Visibility = Visibility.Collapsed;
                errIdNumber1.Visibility = Visibility.Collapsed;

                bool isValid = true;

                // Validate First Name (required)
                if (string.IsNullOrWhiteSpace(txtFirstName.Text))
                {
                    errCustomer1.Visibility = Visibility.Visible;
                    isValid = false;
                }

                // Validate Phone Number (required)
                if (string.IsNullOrWhiteSpace(txtPhone.Text) || txtPhone.Text.Length < 10)
                {
                    errPhone1.Visibility = Visibility.Visible;
                    isValid = false;
                }

                // Validate ID Type (required)
                if (cmbIdType.SelectedItem == null || cmbIdType.SelectedItem == idPlaceholder)
                {
                    errIdType1.Visibility = Visibility.Visible;
                    isValid = false;
                }

                // Validate ID Number (required and format)
                if (!ValidateIdNumber())
                {
                    errIdNumber1.Visibility = Visibility.Visible;
                    isValid = false;
                }

                // Validate at least one luggage item is filled
                var filledRows = GetFilledRowsCount();
                if (filledRows == 0)
                {
                    MessageBox.Show("Please add at least one luggage item before generating the bill.",
                                   "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    isValid = false;
                }

                // If validation fails, don't generate bill
                if (!isValid)
                {
                    MessageBox.Show("Please fill all required fields correctly before generating the bill.",
                                   "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Generate bill if all validations pass
                var totalAmount = GetTotalAmount();

                string customerName = txtFirstName.Text.Trim();
                if (!string.IsNullOrWhiteSpace(txtLastName.Text))
                {
                    customerName += " " + txtLastName.Text.Trim();
                }

                string idType = ((ComboBoxItem)cmbIdType.SelectedItem).Content.ToString();

                // Create booking object to save to database
                string bookingId = GenerateBookingId();
                string workerId = LocalStorage.GetItem("worker_id") ?? LocalStorage.GetItem("username") ?? "LUGGAGE";
                
                // Create luggage items description from the items
                var filledItems = Items.Where(item => !string.IsNullOrWhiteSpace(item.LuggageType) && item.Quantity > 0).ToList();
                string luggageDescription = string.Join(", ", filledItems.Select(item => $"{item.LuggageType} x{item.Quantity}"));

                var booking = new Models.Booking1
                {
                    booking_id = bookingId,
                    worker_id = workerId,
                    guest_name = customerName,
                    phone_number = txtPhone.Text.Trim(),
                    number_of_persons = filledItems.Sum(item => item.Quantity), // Total number of luggage items
                    booking_type = $"Luggage ({luggageDescription})", // Store luggage details in booking_type
                    total_hours = 0, // Luggage doesn't have hours concept
                    booking_date = DateTime.TryParse(txtBookingDate.Text, out DateTime bookingDate) ? bookingDate : DateTime.Now,
                    in_time = DateTime.TryParse(txtBookingTime.Text, out DateTime bookingTime) ? bookingTime.TimeOfDay : DateTime.Now.TimeOfDay,
                    out_time = null, // Will be set when luggage is collected
                    proof_type = idType,
                    proof_id = txtIdNumber.Text.Trim(),
                    price_per_person = totalAmount / Math.Max(1, filledItems.Sum(item => item.Quantity)), // Average price per item
                    total_amount = totalAmount,
                    paid_amount = totalAmount, // Luggage is typically paid upfront
                    balance_amount = 0,
                    payment_method = "Cash",
                    created_at = DateTime.Now,
                    updated_at = DateTime.Now,
                    status = "active", // Active until luggage is collected
                    IsSynced = 0
                };

                // Save booking to database
                await OfflineBookingStorage.SaveBookingAsync(booking, showMessages: false);
                Logger.Log($"Luggage booking saved: {bookingId} for {customerName}");

                // Show success message with booking details
                string summary = $"LUGGAGE BOOKING BILL\n" +
                               $"====================\n\n" +
                               $"Booking ID: {bookingId}\n" +
                               $"Customer: {customerName}\n" +
                               $"Phone: {txtPhone.Text}\n" +
                               $"ID Type: {idType}\n" +
                               $"ID Number: {txtIdNumber.Text}\n" +
                               $"Date: {txtBookingDate.Text}\n" +
                               $"Time: {txtBookingTime.Text}\n\n" +
                               $"ITEMS:\n" +
                               $"------\n";

                foreach (var item in filledItems)
                {
                    summary += $"{item.SNo}. {item.LuggageType} x {item.Quantity} = ₹{item.TotalAmount:F2}\n";
                }

                summary += $"\n==============================\n";
                summary += $"Total Items: {filledRows}\n";
                summary += $"TOTAL AMOUNT: ₹{totalAmount:F2}\n";
                summary += $"==============================";

                MessageBox.Show(summary, "Bill Generated Successfully", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh Dashboard to show the new booking
                RefreshDashboard();

                // Clear the form for next booking
                ClearForm();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GenerateBill_Click: {ex.Message}");
                Logger.LogError(ex);
                MessageBox.Show("Error generating bill. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Generate unique booking ID
        private string GenerateBookingId()
        {
            return "LUG" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999);
        }

        // Refresh Dashboard after saving booking
        private void RefreshDashboard()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null && mainWindow.MainContent.Content is Header header)
                {
                    if (header.MainContentHost.Content is Dashboard dashboard)
                    {
                        dashboard.LoadBookings();
                        dashboard.UpdateCountsFromBookings();
                        Logger.Log("Dashboard refreshed after luggage booking");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        // Clear form after successful booking
        private void ClearForm()
        {
            try
            {
                txtFirstName.Text = string.Empty;
                txtLastName.Text = string.Empty;
                txtPhone.Text = string.Empty;
                txtIdNumber.Text = string.Empty;
                cmbIdType.SelectedItem = idPlaceholder;
                
                // Clear locker fields
                txtLockerCount.Text = string.Empty;
                if (cmbLockerNumber != null)
                {
                    cmbLockerNumber.SelectedIndex = -1;
                    cmbLockerNumber.Text = string.Empty;
                }
                
                // Clear luggage items
                InitializeData();
                
                Logger.Log("Luggage form cleared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        // Method to add a new row manually (can be called from UI)
        public void AddNewRow()
        {
            try
            {
                if (Items.Count < 50)
                {
                    Items.Add(new LuggageItem());
                    System.Diagnostics.Debug.WriteLine($"New row added. Total rows: {Items.Count}");
                }
                else
                {
                    MessageBox.Show("Maximum 50 rows allowed.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddNewRow: {ex.Message}");
            }
        }

        // Method to remove empty rows at the end (keeping at least 3 rows)
        public void RemoveEmptyRows()
        {
            try
            {
                var emptyRows = Items.Where(item => string.IsNullOrWhiteSpace(item.LuggageType) && item.Quantity == 0)
                                    .Skip(1) // Keep at least one empty row
                                    .ToList();

                // Don't remove if it would leave us with less than 3 rows
                if (Items.Count - emptyRows.Count >= 3)
                {
                    foreach (var row in emptyRows)
                    {
                        Items.Remove(row);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Empty rows cleaned. Total rows: {Items.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RemoveEmptyRows: {ex.Message}");
            }
        }

        // Method to get total amount for all items
        public decimal GetTotalAmount()
        {
            try
            {
                return Items.Where(item => !string.IsNullOrWhiteSpace(item.LuggageType) && item.Quantity > 0)
                           .Sum(item => item.TotalAmount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetTotalAmount: {ex.Message}");
                return 0m;
            }
        }

        // Method to get count of filled rows
        public int GetFilledRowsCount()
        {
            try
            {
                return Items.Count(item => !string.IsNullOrWhiteSpace(item.LuggageType) && item.Quantity > 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetFilledRowsCount: {ex.Message}");
                return 0;
            }
        }

        private void txtSeats_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void txtHours_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // Helper to find column index by header text
        private int GetColumnIndexByHeader(DataGrid dg, string headerText)
        {
            try
            {
                if (dg?.Columns == null || string.IsNullOrEmpty(headerText))
                    return -1;

                for (int i = 0; i < dg.Columns.Count; i++)
                {
                    var column = dg.Columns[i];
                    if (column?.Header?.ToString()?.Equals(headerText, StringComparison.OrdinalIgnoreCase) == true)
                        return i;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetColumnIndexByHeader: {ex.Message}");
            }

            return -1;
        }

        // FIXED: Handle Enter key on DataGrid - only manual navigation, no automatic row generation
        private void LuggageGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is DataGrid dg)
            {
                // Check if we're in the quantity column - let the specific handler deal with it
                if (dg.CurrentColumn?.Header?.ToString()?.Equals("Quantity", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Let the Quantity_PreviewKeyDown handler deal with this
                    return;
                }

                e.Handled = true;

                try
                {
                    // Commit current edits
                    dg.CommitEdit(DataGridEditingUnit.Cell, true);
                    dg.CommitEdit(DataGridEditingUnit.Row, true);

                    var currentCell = dg.CurrentCell;

                    // Validate current cell
                    if (currentCell.Item == null || currentCell.Column == null)
                    {
                        return;
                    }

                    int rowIndex = dg.Items.IndexOf(currentCell.Item);

                    // Validate row index
                    if (rowIndex < 0 || rowIndex >= dg.Items.Count)
                    {
                        return;
                    }

                    var header = currentCell.Column.Header?.ToString() ?? string.Empty;

                    // If current column is "Luggage Type", move to "Quantity" in same row
                    if (header.Equals("Luggage Type", StringComparison.OrdinalIgnoreCase))
                    {
                        int qtyIndex = GetColumnIndexByHeader(dg, "Quantity");
                        if (qtyIndex >= 0 && rowIndex >= 0 && rowIndex < dg.Items.Count && qtyIndex < dg.Columns.Count)
                        {
                            var targetItem = dg.Items[rowIndex];
                            if (targetItem != null)
                            {
                                NavigateToCell(dg, targetItem, dg.Columns[qtyIndex]);
                            }
                            return;
                        }
                    }

                    // For other columns, navigate to next editable column/row
                    int colIndex = currentCell.Column.DisplayIndex;
                    int nextCol = colIndex + 1;
                    int nextRowFallback = rowIndex;

                    // Find next editable column in current row
                    while (nextCol < dg.Columns.Count && dg.Columns[nextCol].IsReadOnly)
                    {
                        nextCol++;
                    }

                    // If no editable column found in current row, move to next row
                    if (nextCol >= dg.Columns.Count)
                    {
                        nextCol = 0;
                        nextRowFallback = rowIndex + 1;

                        // Skip to first editable column in next row
                        while (nextCol < dg.Columns.Count && dg.Columns[nextCol].IsReadOnly)
                        {
                            nextCol++;
                        }
                    }

                    // Only add a new row if we're at the end and don't have one
                    if (nextRowFallback >= dg.Items.Count && Items.Count < 50)
                    {
                        Items.Add(new LuggageItem());
                        System.Diagnostics.Debug.WriteLine("Manual row addition via Enter key navigation");
                    }

                    if (nextRowFallback < dg.Items.Count && nextCol < dg.Columns.Count)
                    {
                        var targetItem = dg.Items[nextRowFallback];
                        if (targetItem != null)
                        {
                            NavigateToCell(dg, targetItem, dg.Columns[nextCol]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in LuggageGrid_PreviewKeyDown: {ex.Message}");
                }
            }
        }

        // Helper method to navigate to a cell with proper cursor positioning
        private void NavigateToCell(DataGrid dg, object targetItem, DataGridColumn targetColumn)
        {
            try
            {
                // Set current cell without triggering automatic navigation
                dg.CurrentCell = new DataGridCellInfo(targetItem, targetColumn);
                dg.BeginEdit();

                // Set cursor position for text controls
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (dg.CurrentCell.Column != null && dg.CurrentCell.Item != null)
                    {
                        var cellContent = dg.CurrentCell.Column.GetCellContent(dg.CurrentCell.Item);
                        if (cellContent != null)
                        {
                            // Find the actual input control and set cursor position
                            var textBox = FindVisualChild<TextBox>(cellContent);
                            if (textBox != null)
                            {
                                textBox.Focus();


                                // Special handling for quantity field - clear zero if it's the only content
                                if (dg.CurrentCell.Column.Header?.ToString() == "Quantity" && textBox.Text == "0")
                                {
                                    textBox.Clear(); // Clear the zero
                                }
                                else
                                {
                                    textBox.SelectAll(); // Select all text for easy replacement
                                }
                                textBox.CaretIndex = textBox.Text.Length; // Set cursor at end
                            }

                            var comboBox = FindVisualChild<ComboBox>(cellContent);
                            if (comboBox != null)
                            {
                                comboBox.Focus();
                                if (comboBox.IsEditable)
                                {
                                    var textBoxInCombo = FindVisualChild<TextBox>(comboBox);
                                    if (textBoxInCombo != null)
                                    {
                                        textBoxInCombo.SelectAll();
                                        textBoxInCombo.CaretIndex = textBoxInCombo.Text.Length;
                                    }
                                }
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NavigateToCell: {ex.Message}");
            }
        }

        // Single-click editing - ensure we don't try to select rows
        private void LuggageGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not DataGrid dg) return;

                var dep = (DependencyObject)e.OriginalSource;
                while (dep != null && !(dep is DataGridCell) && !(dep is DataGridColumnHeader))
                    dep = VisualTreeHelper.GetParent(dep);

                if (dep is DataGridCell cell && !cell.IsEditing)
                {
                    // Ensure the cell is valid before trying to focus and edit
                    if (cell.IsEnabled && cell.IsVisible)
                    {
                        // Focus on the cell and begin editing
                        cell.Focus();

                        // Set the current cell without selecting the row
                        var cellInfo = new DataGridCellInfo(cell);
                        dg.CurrentCell = cellInfo;

                        dg.BeginEdit();

                        // Set cursor position after a brief delay
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var textBox = FindVisualChild<TextBox>(cell);
                            if (textBox != null)
                            {
                                textBox.Focus();


                                // Special handling for quantity field - clear zero if it's the only content
                                if (dg.CurrentCell.Column?.Header?.ToString() == "Quantity" && textBox.Text == "0")
                                {
                                    textBox.Clear(); // Clear the zero
                                }
                                else
                                {
                                    textBox.SelectAll();
                                }
                                textBox.CaretIndex = textBox.Text.Length;
                            }

                            var comboBox = FindVisualChild<ComboBox>(cell);
                            if (comboBox != null)
                            {
                                comboBox.Focus();
                                if (comboBox.IsEditable)
                                {
                                    var textBoxInCombo = FindVisualChild<TextBox>(comboBox);
                                    if (textBoxInCombo != null)
                                    {
                                        textBoxInCombo.SelectAll();
                                        textBoxInCombo.CaretIndex = textBoxInCombo.Text.Length;
                                    }
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageGrid_PreviewMouseLeftButtonDown: {ex.Message}");
            }
        }

        // Handle LuggageType GotFocus to set cursor position
        private void LuggageType_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox)
                {
                    // Delay to ensure the control is fully loaded
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (comboBox.IsEditable)
                        {
                            var textBox = FindVisualChild<TextBox>(comboBox);
                            if (textBox != null)
                            {
                                textBox.Focus();
                                textBox.SelectAll(); // Select all text for easy replacement
                                textBox.CaretIndex = textBox.Text.Length; // Set cursor at end after selection
                            }
                        }
                        else
                        {
                            comboBox.Focus();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageType_GotFocus: {ex.Message}");
            }
        }

        // FIXED: Handle Quantity GotFocus to clear zero and set cursor position
        private void Quantity_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    // Delay to ensure the control is fully loaded
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        textBox.Focus();

                        // Clear the zero if it's the only content, otherwise select all
                        if (textBox.Text == "0")
                        {
                            textBox.Clear(); // This will remove the zero
                        }
                        else
                        {
                            textBox.SelectAll(); // Select all text for easy replacement
                        }

                        textBox.CaretIndex = textBox.Text.Length; // Set cursor at end
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Quantity_GotFocus: {ex.Message}");
            }
        }

        private void LuggageType_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.DataContext is LuggageItem item)
                {
                    System.Diagnostics.Debug.WriteLine($"LuggageType_LostFocus: {item.LuggageType}");

                    // Force price calculation
                    if (!string.IsNullOrEmpty(item.LuggageType) && priceMap.TryGetValue(item.LuggageType, out var price))
                    {
                        item.UpdateValues(price, price * item.Quantity, false);
                        System.Diagnostics.Debug.WriteLine($"Updated price: {price}, total: {price * item.Quantity}");
                    }

                    // Force UI refresh
                    comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateTarget();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageType_LostFocus: {ex.Message}");
            }
        }

        // FIXED: Handle Quantity LostFocus to restore zero if empty
        private void Quantity_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox && textBox.DataContext is LuggageItem item)
                {
                    // If the textbox is empty, set quantity back to 0
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = "0";
                        item.Quantity = 0;
                    }

                    System.Diagnostics.Debug.WriteLine($"Quantity_LostFocus: {item.Quantity}");

                    // Force total calculation
                    item.UpdateValues(totalAmount: item.Price * item.Quantity, suppressEvents: false);
                    System.Diagnostics.Debug.WriteLine($"Updated total: {item.TotalAmount}");

                    // Force UI refresh
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Quantity_LostFocus: {ex.Message}");
            }
        }

        // Handle Price LostFocus to update total amount
        private void Price_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox && textBox.DataContext is LuggageItem item)
                {
                    // If the textbox is empty, set price back to 0
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = "0";
                        item.Price = 0;
                    }

                    System.Diagnostics.Debug.WriteLine($"Price_LostFocus: {item.Price}");

                    // Force total calculation based on new price
                    item.UpdateValues(totalAmount: item.Price * item.Quantity, suppressEvents: false);
                    System.Diagnostics.Debug.WriteLine($"Updated total: {item.TotalAmount}");

                    // Force UI refresh
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Price_LostFocus: {ex.Message}");
            }
        }

        // Handle Price GotFocus to select text
        private void Price_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    // Delay to ensure the control is fully loaded
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        textBox.Focus();

                        // Clear the zero if it's the only content, otherwise select all
                        if (textBox.Text == "0")
                        {
                            textBox.Clear();
                        }
                        else
                        {
                            textBox.SelectAll();
                        }

                        textBox.CaretIndex = textBox.Text.Length;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Price_GotFocus: {ex.Message}");
            }
        }

        // Handle Price PreviewKeyDown to update total on Enter
        private void Price_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter && sender is TextBox textBox && textBox.DataContext is LuggageItem item)
                {
                    string priceText = textBox.Text.Trim();
                    if (string.IsNullOrEmpty(priceText))
                    {
                        return;
                    }

                    e.Handled = true;

                    // Commit current edit
                    LuggageGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    LuggageGrid.CommitEdit(DataGridEditingUnit.Row, true);

                    // Parse and update price
                    if (decimal.TryParse(priceText, out decimal price) && price >= 0)
                    {
                        item.Price = price;
                        item.TotalAmount = item.Price * item.Quantity;
                        System.Diagnostics.Debug.WriteLine($"Price updated to: {price}, total: {item.TotalAmount}");

                        // Force UI refresh
                        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();

                        // Move to next cell
                        var nextColIndex = LuggageGrid.CurrentCell.Column.DisplayIndex + 1;
                        if (nextColIndex < LuggageGrid.Columns.Count)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                NavigateToCell(LuggageGrid, item, LuggageGrid.Columns[nextColIndex]);
                            }), System.Windows.Threading.DispatcherPriority.Render);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Price_PreviewKeyDown: {ex.Message}");
            }
        }

        // Handle ComboBox dropdown opened - reset to full list
        private void LuggageType_DropDownOpened(object sender, EventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox)
                {
                    // Reset to full list when dropdown is opened
                    comboBox.ItemsSource = LuggageTypes;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageType_DropDownOpened: {ex.Message}");
            }
        }

        // Handle ComboBox key up for filtering after 2 characters
        private void LuggageType_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox)
                {
                    string searchText = comboBox.Text;

                    // Only filter if user has typed at least 2 characters
                    if (searchText.Length >= 2)
                    {
                        var filteredItems = LuggageTypes.Where(type =>
                            type.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                            type.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (filteredItems.Any())
                        {
                            // Update ItemsSource with filtered results
                            var currentItemsSource = comboBox.ItemsSource as System.Collections.IEnumerable;
                            if (currentItemsSource == null || !filteredItems.SequenceEqual(currentItemsSource.Cast<string>()))
                            {
                                comboBox.ItemsSource = filteredItems;

                                // Open dropdown if not already open
                                if (!comboBox.IsDropDownOpen)
                                {
                                    comboBox.IsDropDownOpen = true;
                                }
                            }
                        }
                    }
                    else if (searchText.Length < 2)
                    {
                        // Reset to full list if less than 2 characters
                        comboBox.ItemsSource = LuggageTypes;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageType_KeyUp: {ex.Message}");
            }
        }

        // FIXED: Handle Enter key specifically for Quantity field - only add row when quantity is properly filled
        private void Quantity_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter && sender is TextBox textBox && textBox.DataContext is LuggageItem item)
                {
                    // Validate that quantity has a value and is 2 characters or less
                    string quantityText = textBox.Text.Trim();
                    if (string.IsNullOrEmpty(quantityText) || quantityText.Length > 2)
                    {
                        // Don't proceed if quantity is empty or longer than 2 characters
                        return;
                    }

                    e.Handled = true;

                    // Commit current edit
                    LuggageGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    LuggageGrid.CommitEdit(DataGridEditingUnit.Row, true);

                    // Force quantity update and total calculation
                    if (int.TryParse(quantityText, out int quantity) && quantity > 0)
                    {
                        item.Quantity = quantity;
                        item.TotalAmount = item.Price * item.Quantity;

                        var itemIndex = Items.IndexOf(item);

                        // Only add a new row if current row is completely filled AND we're at the last row
                        if (itemIndex >= 0 && Items.Count < 50 && IsRowCompletelyFilled(item) && itemIndex == Items.Count - 1)
                        {
                            Items.Add(new LuggageItem());
                            System.Diagnostics.Debug.WriteLine($"New row added via Enter key from quantity. Total rows: {Items.Count}");

                            // Update DataGrid height based on row count for scrolling after 6 rows
                            UpdateDataGridHeight();

                            // Move to the luggage type column of the new row with proper cursor positioning
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var newRowIndex = Items.Count - 1;
                                if (newRowIndex < LuggageGrid.Items.Count)
                                {
                                    var lugTypeColIndex = GetColumnIndexByHeader(LuggageGrid, "Luggage Type");
                                    if (lugTypeColIndex >= 0)
                                    {
                                        var newRowItem = Items[newRowIndex];
                                        NavigateToCell(LuggageGrid, newRowItem, LuggageGrid.Columns[lugTypeColIndex]);

                                        // Scroll to the new row if needed
                                        LuggageGrid.ScrollIntoView(newRowItem);
                                    }
                                }
                            }), System.Windows.Threading.DispatcherPriority.Render);
                        }
                        else
                        {
                            // If row is not complete or not the last row, just move to next cell in same row or next existing row
                            var lugTypeColIndex = GetColumnIndexByHeader(LuggageGrid, "Luggage Type");
                            var nextRowIndex = itemIndex + 1;

                            if (nextRowIndex < Items.Count && lugTypeColIndex >= 0)
                            {
                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    var nextRowItem = Items[nextRowIndex];
                                    NavigateToCell(LuggageGrid, nextRowItem, LuggageGrid.Columns[lugTypeColIndex]);
                                }), System.Windows.Threading.DispatcherPriority.Render);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Quantity_PreviewKeyDown: {ex.Message}");
            }
        }

        // Update DataGrid height based on number of rows - enable hidden scrolling after 6 rows
        private void UpdateDataGridHeight()
        {
            try
            {
                const int rowHeight = 40;
                const int headerHeight = 40;
                const int maxVisibleRows = 6;
                const int padding = 16; // Account for border padding

                int totalRows = Items.Count;
                int visibleRows = Math.Min(totalRows, maxVisibleRows);

                double newHeight = (visibleRows * rowHeight) + headerHeight + padding;

                // Set minimum height for at least 3 rows
                double minHeight = (3 * rowHeight) + headerHeight + padding;
                newHeight = Math.Max(newHeight, minHeight);

                // Set maximum height for 6 rows (then scrolling kicks in)
                double maxHeight = (maxVisibleRows * rowHeight) + headerHeight + padding;
                newHeight = Math.Min(newHeight, maxHeight);

                // Update the DataGrid height
                LuggageGrid.Height = newHeight;

                // Enable scrolling but hide scrollbar - content can scroll but scrollbar is invisible
                // The scrollbar style in XAML makes it completely transparent
                LuggageGrid.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

                System.Diagnostics.Debug.WriteLine($"DataGrid height updated: {newHeight}px for {totalRows} rows, scrollbar hidden");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateDataGridHeight: {ex.Message}");
            }
        }

        // Handle mouse wheel scrolling even with hidden scrollbars
        private void LuggageGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (sender is DataGrid dataGrid)
                {
                    // Find the ScrollViewer inside the DataGrid
                    var scrollViewer = FindVisualChild<ScrollViewer>(dataGrid);
                    if (scrollViewer != null)
                    {
                        // Check if DataGrid needs scrolling
                        if ((e.Delta > 0 && scrollViewer.VerticalOffset > 0) ||
                            (e.Delta < 0 && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight))
                        {
                            // DataGrid has scrollable content, handle it here
                            if (e.Delta > 0)
                            {
                                // Scroll up
                                scrollViewer.LineUp();
                                scrollViewer.LineUp();
                                scrollViewer.LineUp();
                            }
                            else
                            {
                                // Scroll down
                                scrollViewer.LineDown();
                                scrollViewer.LineDown();
                                scrollViewer.LineDown();
                            }
                            e.Handled = true;
                        }
                        else
                        {
                            // DataGrid doesn't need scrolling, let event bubble to MainScrollViewer
                            e.Handled = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageGrid_PreviewMouseWheel: {ex.Message}");
            }
        }

        // Handle mouse wheel scrolling on the main form ScrollViewer
        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (sender is ScrollViewer scrollViewer)
                {
                    // Calculate scroll amount based on wheel delta
                    double scrollAmount = e.Delta > 0 ? -50 : 50; // Negative for up, positive for down
                    
                    // Scroll the main ScrollViewer
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
                    
                    // Mark event as handled to prevent it from bubbling
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainScrollViewer_PreviewMouseWheel: {ex.Message}");
            }
        }

        // Helper method to find visual child of specific type
        private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T result)
                    return result;

                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        // Model
        public class LuggageItem : INotifyPropertyChanged
        {
            private int _sno;
            private string _luggageType = string.Empty;
            private int _quantity;
            private decimal _price;
            private decimal _totalAmount;
            private bool _isUpdating = false; // Flag to prevent recursive updates

            public int SNo
            {
                get => _sno;
                set
                {
                    if (_sno != value && !_isUpdating)
                    {
                        _sno = value;
                        OnPropertyChanged(nameof(SNo));
                    }
                }
            }

            public string LuggageType
            {
                get => _luggageType;
                set
                {
                    var newValue = value ?? string.Empty;
                    if (_luggageType != newValue && !_isUpdating)
                    {
                        _luggageType = newValue;
                        OnPropertyChanged(nameof(LuggageType));
                    }
                }
            }

            public int Quantity
            {
                get => _quantity;
                set
                {
                    var newValue = Math.Max(0, Math.Min(99, value)); // Ensure quantity is 0-99 (2 digits max)
                    if (_quantity != newValue && !_isUpdating)
                    {
                        _quantity = newValue;
                        OnPropertyChanged(nameof(Quantity));
                    }
                }
            }

            public decimal Price
            {
                get => _price;
                set
                {
                    var newValue = Math.Max(0m, value); // Ensure price is not negative
                    if (_price != newValue && !_isUpdating)
                    {
                        _price = newValue;
                        OnPropertyChanged(nameof(Price));
                    }
                }
            }

            public decimal TotalAmount
            {
                get => _totalAmount;
                set
                {
                    var newValue = Math.Max(0m, value); // Ensure total amount is not negative
                    if (_totalAmount != newValue && !_isUpdating)
                    {
                        _totalAmount = newValue;
                        OnPropertyChanged(nameof(TotalAmount));
                    }
                }
            }

            // Method to update multiple properties without triggering events
            public void UpdateValues(decimal? price = null, decimal? totalAmount = null, bool suppressEvents = false)
            {
                try
                {
                    _isUpdating = suppressEvents;

                    if (price.HasValue)
                    {
                        _price = Math.Max(0m, price.Value);
                        if (!suppressEvents) OnPropertyChanged(nameof(Price));
                    }

                    if (totalAmount.HasValue)
                    {
                        _totalAmount = Math.Max(0m, totalAmount.Value);
                        if (!suppressEvents) OnPropertyChanged(nameof(TotalAmount));
                    }
                }
                finally
                {
                    _isUpdating = false;
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected void OnPropertyChanged(string name)
            {
                try
                {
                    if (!_isUpdating)
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in OnPropertyChanged for {name}: {ex.Message}");
                }
            }
        }

        // Handle LuggageType PreviewKeyDown for Enter key dropdown behavior
        private void LuggageType_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter && sender is ComboBox comboBox)
                {
                    if (!comboBox.IsDropDownOpen)
                    {
                        // First Enter: Open the dropdown
                        comboBox.IsDropDownOpen = true;
                        e.Handled = true;

                        // Focus on the dropdown for navigation
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // Set focus to allow arrow key navigation
                            comboBox.Focus();
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                    else
                    {
                        // Second Enter: Select the highlighted item and move to next cell
                        e.Handled = true;
                        comboBox.IsDropDownOpen = false;

                        // Commit the current edit
                        LuggageGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                        LuggageGrid.CommitEdit(DataGridEditingUnit.Row, true);

                        // Move to the next cell (Quantity column in same row)
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var currentCell = LuggageGrid.CurrentCell;
                            if (currentCell.Item != null)
                            {
                                int rowIndex = LuggageGrid.Items.IndexOf(currentCell.Item);
                                int qtyIndex = GetColumnIndexByHeader(LuggageGrid, "Quantity");

                                if (rowIndex >= 0 && qtyIndex >= 0 && qtyIndex < LuggageGrid.Columns.Count)
                                {
                                    var targetItem = LuggageGrid.Items[rowIndex];
                                    NavigateToCell(LuggageGrid, targetItem, LuggageGrid.Columns[qtyIndex]);
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.Render);
                    }
                }
                else if (e.Key == Key.Escape && sender is ComboBox comboBox2)
                {
                    // Escape key: Close dropdown without selecting
                    if (comboBox2.IsDropDownOpen)
                    {
                        comboBox2.IsDropDownOpen = false;
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageType_PreviewKeyDown: {ex.Message}");
            }
        }

        // Form navigation event handlers with validation
        private void FirstName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    e.Handled = true;

                    // Validate first name
                    if (string.IsNullOrWhiteSpace(txtFirstName.Text))
                    {
                        errCustomer1.Visibility = Visibility.Visible;
                        return;
                    }
                    else
                    {
                        errCustomer1.Visibility = Visibility.Collapsed;
                    }

                    txtLastName.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FirstName_PreviewKeyDown: {ex.Message}");
            }
        }

        private void LastName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    // No validation for last name as per requirement
                    txtPhone.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LastName_PreviewKeyDown: {ex.Message}");
            }
        }

        private void Phone_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    e.Handled = true;

                    // Validate phone number
                    if (string.IsNullOrWhiteSpace(txtPhone.Text) || txtPhone.Text.Length < 10)
                    {
                        errPhone1.Visibility = Visibility.Visible;
                        return;
                    }
                    else
                    {
                        errPhone1.Visibility = Visibility.Collapsed;
                    }

                    cmbIdType.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Phone_PreviewKeyDown: {ex.Message}");
            }
        }

        private void IdType_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter && sender is ComboBox comboBox)
                {
                    if (!comboBox.IsDropDownOpen)
                    {
                        // First Enter: Open the dropdown
                        comboBox.IsDropDownOpen = true;
                        e.Handled = true;
                    }
                    else
                    {
                        // Second Enter: Select item and move to next field
                        e.Handled = true;
                        comboBox.IsDropDownOpen = false;

                        // Validate ID type selection
                        if (cmbIdType.SelectedItem == null || cmbIdType.SelectedItem == idPlaceholder)
                        {
                            errIdType1.Visibility = Visibility.Visible;
                            return;
                        }
                        else
                        {
                            errIdType1.Visibility = Visibility.Collapsed;
                        }

                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            txtIdNumber.Focus();
                        }), System.Windows.Threading.DispatcherPriority.Render);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IdType_PreviewKeyDown: {ex.Message}");
            }
        }

        private void IdNumber_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Check for Shift+Enter to trigger Generate Bill
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    e.Handled = true;
                    GenerateBill_Click(sender, new RoutedEventArgs());
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    e.Handled = true;

                    // Validate ID number based on selected ID type
                    if (!ValidateIdNumber())
                    {
                        errIdNumber1.Visibility = Visibility.Visible;
                        return;
                    }
                    else
                    {
                        errIdNumber1.Visibility = Visibility.Collapsed;
                    }

                    // Move focus to locker count field
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        txtLockerCount?.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IdNumber_PreviewKeyDown: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle locker count preview key down for Tab navigation
        /// </summary>
        
        /// <summary>
        /// Handle ComboBox selection changed - add selected locker to the list
        /// </summary>
        private void LockerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbLockerNumber == null || selectedLockersItemsControl == null)
                    return;

                if (cmbLockerNumber.SelectedItem != null)
                {
                    string selectedLocker = cmbLockerNumber.SelectedItem.ToString();
                    string lockerNumber = selectedLocker.Replace("Locker ", "");
                    
                    // Get max allowed selections
                    int maxLockerCount = GetMaxLockerCount();
                    
                    // Check if locker already selected
                    if (selectedLockersList.Any(l => l == lockerNumber))
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Locker {lockerNumber} already selected");
                        cmbLockerNumber.SelectedItem = null;
                        cmbLockerNumber.Text = "";
                        return;
                    }
                    
                    // Check if limit reached
                    if (selectedLockersList.Count >= maxLockerCount)
                    {
                        MessageBox.Show($"You can only select up to {maxLockerCount} lockers.",
                            "Selection Limit Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                        cmbLockerNumber.SelectedItem = null;
                        cmbLockerNumber.Text = "";
                        return;
                    }
                    
                    // Add to selection
                    selectedLockersList.Add(lockerNumber);
                    UpdateSelectedLockersDisplay();
                    
                    System.Diagnostics.Debug.WriteLine($"✓ Locker {lockerNumber} added. Total: {selectedLockersList.Count}/{maxLockerCount}");
                    
                    // Clear ComboBox for next selection
                    cmbLockerNumber.SelectedItem = null;
                    cmbLockerNumber.Text = "";
                    
                    // Update error visibility
                    if (errLockerNumber != null)
                    {
                        errLockerNumber.Visibility = selectedLockersList.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in LockerComboBox_SelectionChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle ComboBox dropdown opened - show all available items, maintain selections
        /// </summary>
        private void LockerComboBox_DropDownOpened(object sender, EventArgs e)
        {
            try
            {
                if (cmbLockerNumber == null)
                    return;

                string searchText = cmbLockerNumber.Text?.Trim() ?? "";
                
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // Show all items when dropdown opens without text
                    cmbLockerNumber.Items.Clear();
                    foreach (var locker in allLockers)
                    {
                        cmbLockerNumber.Items.Add(locker);
                    }
                }
                else
                {
                    // Show filtered results if text already exists
                    FilterLockerComboBox(searchText);
                }
                
                System.Diagnostics.Debug.WriteLine($"✓ ComboBox dropdown opened with {cmbLockerNumber.Items.Count} items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in LockerComboBox_DropDownOpened: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle text input in ComboBox for real-time search filtering
        /// </summary>
        private void LockerComboBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (cmbLockerNumber == null)
                    return;

                // Only allow numbers (0-9)
                if (!NumbersRegex.IsMatch(e.Text))
                {
                    e.Handled = true;
                    return;
                }

                // Get the current text including the new character
                string newText = cmbLockerNumber.Text + e.Text;
                newText = newText.Trim();

                // Allow up to 3 digits (max locker 999)
                if (newText.Length > 3)
                {
                    e.Handled = true;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(newText))
                {
                    // Filter results as user types
                    FilterLockerComboBox(newText);
                    
                    // Auto-select first result
                    if (cmbLockerNumber.Items.Count > 0)
                    {
                        cmbLockerNumber.SelectedIndex = 0;
                    }
                    
                    // Open dropdown if not already open
                    if (!cmbLockerNumber.IsDropDownOpen)
                    {
                        cmbLockerNumber.IsDropDownOpen = true;
                    }
                }
                else
                {
                    // Show all if text is empty
                    cmbLockerNumber.Items.Clear();
                    foreach (var locker in allLockers)
                    {
                        cmbLockerNumber.Items.Add(locker);
                    }
                    if (!cmbLockerNumber.IsDropDownOpen)
                    {
                        cmbLockerNumber.IsDropDownOpen = true;
                    }
                }

                e.Handled = false; // Allow the text to be updated normally
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in LockerComboBox_TextInput: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle key down in ComboBox to enable real-time filtering and Enter key to add
        /// </summary>
        private void LockerComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (cmbLockerNumber == null)
                    return;

                if (e.Key == Key.Enter)
                {
                    // Add current selection on Enter
                    if (cmbLockerNumber.SelectedItem != null)
                    {
                        string selectedLocker = cmbLockerNumber.SelectedItem.ToString();
                        string lockerNumber = selectedLocker.Replace("Locker ", "");
                        
                        // Get max allowed selections
                        int maxLockerCount = GetMaxLockerCount();
                        
                        // Check if already selected
                        if (!selectedLockersList.Any(l => l == lockerNumber) && selectedLockersList.Count < maxLockerCount)
                        {
                            selectedLockersList.Add(lockerNumber);
                            UpdateSelectedLockersDisplay();
                            cmbLockerNumber.SelectedItem = null;
                            cmbLockerNumber.Text = "";
                            e.Handled = true;
                            return;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(cmbLockerNumber.Text))
                    {
                        // If just text is entered, try to add it as a locker if it exists
                        string textInput = cmbLockerNumber.Text.Trim();
                        string lockerKey = $"Locker {textInput}";
                        
                        if (allLockers.Contains(lockerKey))
                        {
                            int maxLockerCount = GetMaxLockerCount();
                            if (!selectedLockersList.Any(l => l == textInput) && selectedLockersList.Count < maxLockerCount)
                            {
                                selectedLockersList.Add(textInput);
                                UpdateSelectedLockersDisplay();
                                cmbLockerNumber.Text = "";
                                cmbLockerNumber.SelectedItem = null;
                                e.Handled = true;
                            }
                        }
                    }
                }
                else if (e.Key == Key.Back)
                {
                    // Handle backspace - filter after deletion
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string searchText = cmbLockerNumber.Text?.Trim() ?? "";
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            FilterLockerComboBox(searchText);
                            if (cmbLockerNumber.Items.Count > 0)
                            {
                                cmbLockerNumber.SelectedIndex = 0;
                            }
                            if (!cmbLockerNumber.IsDropDownOpen)
                            {
                                cmbLockerNumber.IsDropDownOpen = true;
                            }
                        }
                        else
                        {
                            // Show all if text is empty
                            cmbLockerNumber.Items.Clear();
                            foreach (var locker in allLockers)
                            {
                                cmbLockerNumber.Items.Add(locker);
                            }
                            if (!cmbLockerNumber.IsDropDownOpen)
                            {
                                cmbLockerNumber.IsDropDownOpen = true;
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in LockerComboBox_KeyDown: {ex.Message}");
            }
        }

        /// <summary>
        /// Get maximum allowed locker count from the locker count field
        /// </summary>
        private int GetMaxLockerCount()
        {
            int maxLockerCount = 60; // Default to all
            if (txtLockerCount != null && !string.IsNullOrWhiteSpace(txtLockerCount.Text))
            {
                if (int.TryParse(txtLockerCount.Text, out int count))
                {
                    maxLockerCount = Math.Max(1, Math.Min(60, count));
                }
            }
            return maxLockerCount;
        }

        /// <summary>
        /// Update the display of selected locker numbers with remove buttons
        /// </summary>
        private void UpdateSelectedLockersDisplay()
        {
            try
            {
                if (selectedLockersItemsControl == null)
                    return;

                // Create items source from selected lockers
                var displayList = new ObservableCollection<string>();
                foreach (var lockerNum in selectedLockersList.OrderBy(l => int.TryParse(l, out int n) ? n : 999))
                {
                    displayList.Add(lockerNum);
                }

                selectedLockersItemsControl.ItemsSource = displayList;

                // Update error and warning messages
                int maxLockerCount = GetMaxLockerCount();
                
                if (errLockerNumber != null)
                {
                    errLockerNumber.Visibility = selectedLockersList.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                }
                
                if (lockerLimitWarning != null)
                {
                    if (selectedLockersList.Count >= maxLockerCount && selectedLockersList.Count < 60)
                    {
                        lockerLimitWarning.Text = $"Selection limit reached ({selectedLockersList.Count}/{maxLockerCount})";
                        lockerLimitWarning.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        lockerLimitWarning.Visibility = Visibility.Collapsed;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Display updated with {selectedLockersList.Count} selected lockers");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateSelectedLockersDisplay: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle remove button click in selected lockers display
        /// </summary>
        private void RemoveLockerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string lockerNumber)
                {
                    selectedLockersList.Remove(lockerNumber);
                    UpdateSelectedLockersDisplay();
                    System.Diagnostics.Debug.WriteLine($"✓ Locker {lockerNumber} removed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RemoveLockerButton_Click: {ex.Message}");
            }
        }

        /// <summary>
        /// Filter ComboBox items based on search text - shows suggestions with numeric locker numbers
        /// </summary>
        private void FilterLockerComboBox(string searchText)
        {
            try
            {
                if (cmbLockerNumber == null || string.IsNullOrWhiteSpace(searchText))
                    return;

                // Normalize search text - handle both "Locker 1" and "1" formats
                searchText = searchText.Trim();
                
                // Remove "locker" prefix if user typed it (case insensitive)
                string cleanSearchText = searchText.ToLower().Replace("locker", "").Trim();
                
                // If empty after cleaning, show all
                if (string.IsNullOrWhiteSpace(cleanSearchText))
                {
                    cmbLockerNumber.Items.Clear();
                    foreach (var locker in allLockers)
                    {
                        cmbLockerNumber.Items.Add(locker);
                    }
                    return;
                }

                // Clear and filter
                cmbLockerNumber.Items.Clear();

                var filtered = allLockers
                    .Where(locker =>
                    {
                        // Extract just the number from "Locker X"
                        string lockerNum = locker.Replace("Locker ", "").Trim();
                        
                        // Check if number starts with or contains the search text
                        // Only match if it's a numeric match at the beginning (for "123" to match "Locker 123" but not "Locker 5123")
                        return lockerNum.StartsWith(cleanSearchText, StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(locker =>
                    {
                        string lockerNum = locker.Replace("Locker ", "").Trim();
                        
                        // Exact match gets priority 0
                        if (lockerNum.Equals(cleanSearchText, StringComparison.OrdinalIgnoreCase))
                            return (0, int.Parse(lockerNum));
                        // Starts with search gets priority 1 (already filtered above)
                        else
                            return (1, int.Parse(lockerNum));
                    })
                    .Select(x => x).ToList();

                // Add filtered results to ComboBox (they already have "Locker" prefix)
                foreach (var locker in filtered.OrderBy(l => 
                {
                    int num = int.Parse(l.Replace("Locker ", ""));
                    return num;
                }))
                {
                    cmbLockerNumber.Items.Add(locker);
                }

                System.Diagnostics.Debug.WriteLine($"✓ Filtered {filtered.Count} suggestions for input '{searchText}' (numeric: '{cleanSearchText}')");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in FilterLockerComboBox: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle locker count text change - validate and update error display
        /// </summary>
        private void LockerCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (txtLockerCount == null || errLockerCount == null)
                    return;

                if (ValidateLockerCount())
                {
                    errLockerCount.Visibility = Visibility.Collapsed;
                    System.Diagnostics.Debug.WriteLine($"✓ Valid locker count: {txtLockerCount.Text}");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(txtLockerCount.Text))
                        errLockerCount.Visibility = Visibility.Visible;
                    else
                        errLockerCount.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LockerCount_TextChanged: {ex.Message}");
            }
        }

        private bool ValidateLockerCount()
        {
            try
            {
                if (txtLockerCount == null || string.IsNullOrWhiteSpace(txtLockerCount.Text))
                    return false;

                if (!int.TryParse(txtLockerCount.Text, out int lockerCount))
                    return false;

                return lockerCount >= 1 && lockerCount <= 60;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ValidateLockerCount: {ex.Message}");
                return false;
            }
        }

        private bool ValidateIdNumber()
        {
            try
            {
                if (cmbIdType.SelectedItem == null || cmbIdType.SelectedItem == idPlaceholder)
                    return false;

                string selectedIdType = ((ComboBoxItem)cmbIdType.SelectedItem).Content.ToString();
                string idValue = txtIdNumber.Text.Trim();

                switch (selectedIdType)
                {
                    case "Aadhar":
                        return !string.IsNullOrWhiteSpace(idValue) && idValue.Length == 12 && idValue.All(char.IsDigit);

                    case "PNR Number":
                        return !string.IsNullOrWhiteSpace(idValue) && idValue.Length == 10 && idValue.All(char.IsDigit);

                    case "PAN ID":
                        return !string.IsNullOrWhiteSpace(idValue) && idValue.Length == 10 &&
                               Regex.IsMatch(idValue, @"^[A-Za-z]{5}[0-9]{4}[A-Za-z]{1}$");

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ValidateIdNumber: {ex.Message}");
                return false;
            }
        }

        private void IdNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (cmbIdType.SelectedItem == null || cmbIdType.SelectedItem == idPlaceholder)
                {
                    e.Handled = true;
                    return;
                }

                string selectedIdType = ((ComboBoxItem)cmbIdType.SelectedItem).Content.ToString();

                switch (selectedIdType)
                {
                    case "Aadhar":
                    case "PNR Number":
                        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
                        break;

                    case "PAN ID":
                        string currentText = txtIdNumber.Text;
                        int caretIndex = txtIdNumber.CaretIndex;

                        if (caretIndex < 5)
                        {
                            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z]+$");

                            if (!e.Handled)
                            {
                                e.Handled = true;
                                string upperText = e.Text.ToUpper();
                                int currentCaretIndex = txtIdNumber.CaretIndex;
                                string newText = currentText.Insert(currentCaretIndex, upperText);
                                txtIdNumber.Text = newText;
                                txtIdNumber.CaretIndex = currentCaretIndex + upperText.Length;
                            }
                        }
                        else if (caretIndex >= 5 && caretIndex < 9)
                        {
                            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
                        }
                        else if (caretIndex == 9)
                        {
                            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z]+$");

                            if (!e.Handled)
                            {
                                e.Handled = true;
                                string upperText = e.Text.ToUpper();
                                int currentCaretIndex = txtIdNumber.CaretIndex;
                                string newText = currentText.Insert(currentCaretIndex, upperText);
                                txtIdNumber.Text = newText;
                                txtIdNumber.CaretIndex = currentCaretIndex + upperText.Length;
                            }
                        }
                        break;

                    default:
                        e.Handled = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IdNumber_PreviewTextInput: {ex.Message}");
            }
        }

        private void IdNumber_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                if (cmbIdType.SelectedItem == null || cmbIdType.SelectedItem == idPlaceholder)
                {
                    e.CancelCommand();
                    return;
                }

                if (e.DataObject.GetDataPresent(DataFormats.Text))
                {
                    string pastedText = (e.DataObject.GetData(DataFormats.Text) as string) ?? string.Empty;
                    string selectedIdType = ((ComboBoxItem)cmbIdType.SelectedItem).Content.ToString();

                    bool isValid = selectedIdType switch
                    {
                        "Aadhar" => Regex.IsMatch(pastedText, @"^[0-9]{12}$"),
                        "PNR Number" => Regex.IsMatch(pastedText, @"^[0-9]{10}$"),
                        "PAN ID" => Regex.IsMatch(pastedText, @"^[a-zA-Z]{5}[0-9]{4}[a-zA-Z]{1}$"),
                        _ => false
                    };

                    if (!isValid)
                    {
                        e.CancelCommand();
                    }
                    else if (selectedIdType == "PAN ID")
                    {
                        e.CancelCommand();
                        txtIdNumber.Text = pastedText.ToUpper();
                        txtIdNumber.CaretIndex = txtIdNumber.Text.Length;
                    }
                }
                else
                {
                    e.CancelCommand();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IdNumber_Pasting: {ex.Message}");
                e.CancelCommand();
            }
        }

        private void cmbIdType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbIdType.SelectedItem != null && cmbIdType.SelectedItem != idPlaceholder)
                {
                    string selectedIdType = ((ComboBoxItem)cmbIdType.SelectedItem).Content.ToString();
                    errIdType1.Visibility = Visibility.Collapsed;

                    switch (selectedIdType)
                    {
                        case "Aadhar":
                            txtIdNumber.MaxLength = 12;
                            txtIdNumber.Tag = "Enter 12 digit Aadhar number";
                            lblIdInput.Text = "Enter Aadhar Number";
                            break;

                        case "PNR Number":
                            txtIdNumber.MaxLength = 10;
                            txtIdNumber.Tag = "Enter 10 digit PNR number";
                            lblIdInput.Text = "Enter PNR Number";
                            break;

                        case "PAN ID":
                            txtIdNumber.MaxLength = 10;
                            txtIdNumber.Tag = "Enter PAN ID (ABCDE1234F)";
                            lblIdInput.Text = "Enter PAN ID";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in cmbIdType_SelectionChanged: {ex.Message}");
            }
        }

        private void RoomNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Placeholder for backward compatibility
        }

        /// <summary>
        /// Helper method to find a parent element of a specific type in the visual tree
        /// </summary>
        private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }

        /// <summary>
        /// Handles mouse wheel scrolling for the entire UserControl's ScrollViewer
        /// </summary>
        private void UserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (sender is ScrollViewer scrollViewer)
                {
                    // Calculate scroll amount based on wheel delta
                    double scrollAmount = e.Delta > 0 ? -50 : 50;
                    
                    // Scroll the FormScrollViewer
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
                    
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UserControl_PreviewMouseWheel: {ex.Message}");
            }
        }
    }
}
