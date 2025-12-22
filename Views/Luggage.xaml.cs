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

namespace UserModule.Views
{
    public partial class Luggage : UserControl
    {
        private static readonly Regex LettersRegex = new Regex("^[a-zA-Z ]+$");
        private static readonly Regex NumbersRegex = new Regex("^[0-9]+$");

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
        private void UserControl_Loaded(object sender, RoutedEventArgs e) { }

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
        private void GenerateBill_Click(object sender, RoutedEventArgs e)
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

                string summary = $"LUGGAGE BOOKING BILL\n" +
                               $"====================\n\n" +
                               $"Customer: {customerName}\n" +
                               $"Phone: {txtPhone.Text}\n" +
                               $"ID Type: {idType}\n" +
                               $"ID Number: {txtIdNumber.Text}\n" +
                               $"Date: {txtBookingDate.Text}\n" +
                               $"Time: {txtBookingTime.Text}\n\n" +
                               $"ITEMS:\n" +
                               $"------\n";

                var filledItems = Items.Where(item => !string.IsNullOrWhiteSpace(item.LuggageType) && item.Quantity > 0);

                foreach (var item in filledItems)
                {
                    summary += $"{item.SNo}. {item.LuggageType} x {item.Quantity} = ₹{item.TotalAmount:F2}\n";
                }

                summary += $"\n==============================\n";
                summary += $"Total Items: {filledRows}\n";
                summary += $"TOTAL AMOUNT: ₹{totalAmount:F2}\n";
                summary += $"==============================";

                MessageBox.Show(summary, "Bill Generated Successfully", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GenerateBill_Click: {ex.Message}");
                MessageBox.Show("Error generating bill. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        // Handle vertical scrolling
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LuggageGrid_PreviewMouseWheel: {ex.Message}");
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

                    // Move focus to the first luggage type cell in the grid
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (LuggageGrid.Items.Count > 0)
                        {
                            var firstItem = LuggageGrid.Items[0];
                            var lugTypeColIndex = GetColumnIndexByHeader(LuggageGrid, "Luggage Type");

                            if (lugTypeColIndex >= 0)
                            {
                                NavigateToCell(LuggageGrid, firstItem, LuggageGrid.Columns[lugTypeColIndex]);
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IdNumber_PreviewKeyDown: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle room count preview key down for Tab navigation
        /// </summary>
        private void RoomCount_PreviewKeyDown(object sender, KeyEventArgs e)
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

                    // Validate room count
                    if (!ValidateRoomCount())
                    {
                        if (errRoomCount != null)
                            errRoomCount.Visibility = Visibility.Visible;
                        return;
                    }
                    else
                    {
                        if (errRoomCount != null)
                            errRoomCount.Visibility = Visibility.Collapsed;
                    }

                    // Null-safety check before focusing
                    if (txtRoomNumbers == null)
                        return;

                    // Move focus to room number field
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        txtRoomNumbers?.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RoomCount_PreviewKeyDown: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle room number preview key down for Tab navigation
        /// </summary>
        private void RoomNumber_PreviewKeyDown(object sender, KeyEventArgs e)
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

                    // Null-safety checks
                    if (roomNumberPopup == null || lstRoomNumbers == null)
                        return;

                    if (!roomNumberPopup.IsOpen)
                    {
                        // First Enter: Open the popup
                        if (ValidateRoomCount())
                        {
                            // Ensure items are populated before opening
                            EnsureRoomNumbersPopulated();
                            roomNumberPopup.IsOpen = true;
                            System.Diagnostics.Debug.WriteLine("Enter pressed - popup opened");
                        }
                        else if (errRoomCount != null)
                        {
                            errRoomCount.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        // Second Enter: Close popup and move to luggage grid
                        roomNumberPopup.IsOpen = false;
                        System.Diagnostics.Debug.WriteLine("Enter pressed - popup closed, moving to luggage grid");

                        // Validate at least one room is selected
                        if (lstRoomNumbers.SelectedItems.Count == 0)
                        {
                            if (errRoomNumber != null)
                                errRoomNumber.Visibility = Visibility.Visible;
                            return;
                        }
                        else
                        {
                            if (errRoomNumber != null)
                                errRoomNumber.Visibility = Visibility.Collapsed;
                        }

                        // Null-safety check for LuggageGrid
                        if (LuggageGrid == null)
                            return;

                        // Move focus to the first luggage type cell in the grid
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (LuggageGrid.Items.Count > 0)
                                {
                                    var firstItem = LuggageGrid.Items[0];
                                    var lugTypeColIndex = GetColumnIndexByHeader(LuggageGrid, "Luggage Type");

                                    if (lugTypeColIndex >= 0 && lugTypeColIndex < LuggageGrid.Columns.Count)
                                    {
                                        // Set focus to the DataGrid first
                                        LuggageGrid.Focus();
                    
                                        // Navigate to the Luggage Type cell
                                        NavigateToCell(LuggageGrid, firstItem, LuggageGrid.Columns[lugTypeColIndex]);
                    
                                        System.Diagnostics.Debug.WriteLine($"✓ Navigated to Luggage Type cell in row 0");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error navigating to luggage grid: {ex.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Render);
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    // Escape key: Close popup
                    if (roomNumberPopup != null && roomNumberPopup.IsOpen)
                    {
                        roomNumberPopup.IsOpen = false;
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RoomNumber_PreviewKeyDown: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle room count text changed - validate and populate room numbers
        /// </summary>
        private void RoomCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                // Null-safety check
                if (errRoomCount != null)
                    errRoomCount.Visibility = Visibility.Collapsed;

                // Null-safety checks for ListBox controls
                if (lstRoomNumbers == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: lstRoomNumbers is NULL in RoomCount_TextChanged");
                    return;
                }

                // Clear existing room numbers and selections
                lstRoomNumbers.Items.Clear();
                lstRoomNumbers.SelectedItems.Clear();
                if (txtRoomNumbers != null)
                    txtRoomNumbers.Text = string.Empty;

                if (string.IsNullOrWhiteSpace(txtRoomCount.Text))
                {
                    System.Diagnostics.Debug.WriteLine("Room count is empty - not populating rooms");
                    return;
                }

                if (!int.TryParse(txtRoomCount.Text, out int roomCount))
                {
                    if (errRoomCount != null)
                        errRoomCount.Visibility = Visibility.Visible;
                    System.Diagnostics.Debug.WriteLine($"Invalid room count: {txtRoomCount.Text}");
                    return;
                }

                // Validate room count range
                if (roomCount < 1 || roomCount > 60)
                {
                    if (errRoomCount != null)
                        errRoomCount.Visibility = Visibility.Visible;
                    System.Diagnostics.Debug.WriteLine($"Room count out of range: {roomCount}");
                    return;
                }

                // Populate room numbers from 1 to 60 (total available rooms)
                for (int i = 1; i <= 60; i++)
                {
                    lstRoomNumbers.Items.Add($"Room {i}");
                }

                System.Diagnostics.Debug.WriteLine($"✓ Successfully populated {lstRoomNumbers.Items.Count} room options (user can select up to {roomCount} rooms)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in RoomCount_TextChanged: {ex.Message}");
                if (errRoomCount != null)
                    errRoomCount.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Handle room number border mouse enter to open popup on hover
        /// </summary>
        private void RoomNumberBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                // Only open if room count is valid and popup is not already open
                if (ValidateRoomCount() && roomNumberPopup != null && lstRoomNumbers != null && !roomNumberPopup.IsOpen)
                {
                    // Ensure items are populated before opening
                    EnsureRoomNumbersPopulated();
                    roomNumberPopup.IsOpen = true;
                    
                    System.Diagnostics.Debug.WriteLine("Room numbers field hovered - popup opened");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RoomNumberBorder_MouseEnter: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle room numbers mouse down to open popup
        /// </summary>
        private void RoomNumbers_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Prevent the event from bubbling
                e.Handled = true;
                
                // Only open if room count is valid
                if (ValidateRoomCount() && roomNumberPopup != null && lstRoomNumbers != null)
                {
                    // Ensure items are populated before opening
                    EnsureRoomNumbersPopulated();
                    
                    // Toggle the popup
                    if (!roomNumberPopup.IsOpen)
                    {
                        roomNumberPopup.IsOpen = true;
                        System.Diagnostics.Debug.WriteLine("Room numbers textbox clicked - popup opened");
                    }
                }
                else
                {
                    if (errRoomCount != null)
                        errRoomCount.Visibility = Visibility.Visible;
                    
                    MessageBox.Show("Please enter a valid room count (1-60) first.", 
                        "Room Count Required", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RoomNumbers_MouseDown: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle dropdown button click to open room selection popup
        /// </summary>
        private void RoomNumberDropdown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Only open if room count is valid
                if (ValidateRoomCount() && roomNumberPopup != null && lstRoomNumbers != null)
                {
                    // Ensure items are populated before opening
                    EnsureRoomNumbersPopulated();
                    
                    // Toggle the popup
                    roomNumberPopup.IsOpen = !roomNumberPopup.IsOpen;
                    
                    System.Diagnostics.Debug.WriteLine($"Room dropdown clicked - Popup is now {(roomNumberPopup.IsOpen ? "open" : "closed")}");
                }
                else
                {
                    // Show error if room count is not valid
                    if (errRoomCount != null)
                        errRoomCount.Visibility = Visibility.Visible;
                    
                    MessageBox.Show("Please enter a valid room count (1-60) first.", 
                        "Room Count Required", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RoomNumberDropdown_Click: {ex.Message}");
            }
        }

        private bool ValidateRoomCount()
        {
            try
            {
                if (txtRoomCount == null || string.IsNullOrWhiteSpace(txtRoomCount.Text))
                    return false;

                if (!int.TryParse(txtRoomCount.Text, out int roomCount))
                    return false;

                return roomCount >= 1 && roomCount <= 60;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ValidateRoomCount: {ex.Message}");
                return false;
            }
        }

        private void EnsureRoomNumbersPopulated()
        {
            try
            {
                if (lstRoomNumbers == null || txtRoomCount == null)
                    return;

                if (lstRoomNumbers.Items.Count != 60)
                {
                    var currentSelections = new System.Collections.Generic.List<string>();
                    if (lstRoomNumbers.SelectedItems.Count > 0)
                    {
                        currentSelections.AddRange(lstRoomNumbers.SelectedItems.Cast<string>());
                    }

                    lstRoomNumbers.Items.Clear();
                    
                    for (int i = 1; i <= 60; i++)
                    {
                        lstRoomNumbers.Items.Add($"Room {i}");
                    }

                    if (currentSelections.Count > 0)
                    {
                        foreach (var selection in currentSelections)
                        {
                            var item = lstRoomNumbers.Items.Cast<string>()
                                .FirstOrDefault(s => s == selection);
                            if (item != null)
                            {
                                lstRoomNumbers.SelectedItems.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EnsureRoomNumbersPopulated: {ex.Message}");
            }
        }

        private void RoomNumbers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (lstRoomNumbers == null || txtRoomNumbers == null || txtRoomCount == null)
                    return;

                if (!int.TryParse(txtRoomCount.Text, out int maxRoomCount))
                    return;

                if (lstRoomNumbers.SelectedItems.Count > maxRoomCount)
                {
                    if (e.AddedItems.Count > 0)
                    {
                        lstRoomNumbers.SelectedItems.Remove(e.AddedItems[0]);
                        MessageBox.Show($"You can only select up to {maxRoomCount} rooms based on the room count.",
                            "Selection Limit", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                if (lstRoomNumbers.SelectedItems.Count > 0)
                {
                    var selectedRooms = lstRoomNumbers.SelectedItems.Cast<string>()
                        .Select(s => s.Replace("Room ", ""))
                        .OrderBy(int.Parse)
                        .ToList();
                    
                    txtRoomNumbers.Text = string.Join(", ", selectedRooms.Select(r => $"Room {r}"));
                    
                    if (errRoomNumber != null)
                        errRoomNumber.Visibility = Visibility.Collapsed;
                }
                else
                {
                    txtRoomNumbers.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RoomNumbers_SelectionChanged: {ex.Message}");
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
    }
}
