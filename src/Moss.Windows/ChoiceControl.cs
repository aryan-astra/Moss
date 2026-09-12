using System;
using System.Linq;
using System.Windows.Forms;

namespace Moss.Windows;

internal static class ChoiceControl
{
	public static ComboBox Create<T>(T value) where T : struct, Enum
	{
		ComboBox comboBox = new ComboBox();
		comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		comboBox.Items.AddRange(Enum.GetValues<T>().Cast<object>().ToArray());
		comboBox.SelectedItem = value;
		return comboBox;
	}
}
