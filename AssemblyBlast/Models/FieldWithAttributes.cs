using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AssemblyBlast.Models;

/// <summary>
///     Specifies metadata for a property that will be displayed as a control in a dynamically generated form or dialog.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FieldWithAttributes : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FieldWithAttributes" /> class with the specified control type and
    ///     optional properties.
    /// </summary>
    /// <param name="controlType">The type of UI control to use for this field.</param>
    /// <param name="placeholder">Optional placeholder text to display within the control.</param>
    /// <param name="isRequired">Indicates if the field is required.</param>
    /// <param name="optionsJson">
    ///     A JSON-formatted string representing a collection of selectable options for the control.
    ///     Deserialized into an <see cref="IEnumerable{T}" /> of strings, useful for ComboBox / RadioButton-style controls.
    /// </param>
    /// <param name="controlParametersJson">
    ///     A JSON-formatted string of additional configuration parameters specific to the control type.
    ///     Deserialized into a <see cref="Dictionary{TKey,TValue}" /> of string-object pairs (e.g. MaxLength, Min, Max).
    /// </param>
    /// <param name="isKeyField">Indicates if the field is a logical key (used for lookup, storage, and so on).</param>
    /// <param name="isDisplayField">Indicates if the field is a display field (for listing).</param>
    /// <param name="dataSetControlsJson">
    ///     A JSON-formatted string of dataset-level configurations such as grouping or aggregation settings.
    ///     Deserialized into a <see cref="Dictionary{TKey, TValue}" /> of string-object pairs.
    /// </param>
    public FieldWithAttributes(
        string controlType,
        string placeholder = "",
        bool isRequired = false,
        string optionsJson = "[]",
        string controlParametersJson = "{}",
        bool isKeyField = false,
        bool isDisplayField = false,
        string dataSetControlsJson = "{}")
    {
        ControlType = controlType;
        Placeholder = placeholder;
        IsRequired = isRequired;
        IsKeyField = isKeyField;
        IsDisplayField = isDisplayField;

        Options = JsonSerializer.Deserialize<IEnumerable<string>>(optionsJson) ?? [];
        ControlParameters = JsonSerializer.Deserialize<Dictionary<string, object>>(controlParametersJson) ?? [];
        DataSetControls = JsonSerializer.Deserialize<Dictionary<string, object>>(dataSetControlsJson) ?? [];
    }

    /// <summary>
    ///     Gets the type of UI control to display for this field (e.g., "TextBox", "ComboBox").
    /// </summary>
    public string ControlType { get; }

    /// <summary>
    ///     Gets the placeholder text to display in the UI control, if applicable.
    /// </summary>
    public string Placeholder { get; }

    /// <summary>
    ///     Gets a value indicating whether the field is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    ///     Gets the options for controls like ComboBox or RadioButton.
    /// </summary>
    public IEnumerable<string> Options { get; }

    /// <summary>
    ///     Gets the additional parameters specific to the control type.
    /// </summary>
    /// <remarks>
    ///     The parameters are deserialized from a JSON string into a dictionary of key-value pairs. These parameters
    ///     may include settings like "MaxLength" for a TextBox or "Min" and "Max" values for a NumericUpDown control.
    /// </remarks>
    public Dictionary<string, object> ControlParameters { get; }

    /// <summary>
    ///     Gets a value indicating whether the field represents a unique key for the entity.
    /// </summary>
    /// <remarks>
    ///     Fields marked as key fields are used in entity storage and retrieval operations,
    ///     such as identifying objects in a file-based or database-backed store.
    ///     This is distinct from <see cref="IsDisplayField"/>, which is intended for UI purposes only.
    /// </remarks>
    public bool IsKeyField { get; }

    /// <summary>
    ///     Gets a value indicating whether the field is part of the display name (for listing purposes).
    /// </summary>
    /// <remarks>
    ///     Fields marked as display fields are typically included in summary or listing views of the dataset.
    /// </remarks>
    public bool IsDisplayField { get; }

    /// <summary>
    ///     Gets the dataset-level configuration settings.
    /// </summary>
    /// <remarks>
    ///     This field is designed to hold configurations for grouping, aggregation, filtering, or other
    ///     dataset-wide operations. These settings are deserialized from a JSON string into a dictionary of key-value pairs.
    /// </remarks>
    public Dictionary<string, object> DataSetControls { get; }
}
