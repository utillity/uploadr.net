using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace uTILLIty.UploadrNet.Windows
{
	/// <summary>
	///   A default implemenation of the <see cref="INotifyPropertyChanged" /> contract
	/// </summary>
	public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
	{
		private readonly Dictionary<string, object> _propValues = new Dictionary<string, object>();

		/// <summary>
		///   The event raised when a property of the inheriting instance has changed
		/// </summary>
		public virtual event PropertyChangedEventHandler PropertyChanged;

		public bool HasPropertyBeenSet(string propertyName)
		{
			// ReSharper disable once InconsistentlySynchronizedField
			return _propValues.ContainsKey(propertyName);
		}

		/// <summary>
		///   Raises the <see cref="PropertyChanged" /> event on the UI thread
		/// </summary>
		// ReSharper disable once MemberCanBeProtected.Global
		protected internal virtual void RaisePropertyChanged([CallerMemberName] string propName = null)
		{
			var ev = PropertyChanged;
			if (ev == null || string.IsNullOrEmpty(propName))
				return;

			var args = new PropertyChangedEventArgs(propName);
			ev(this, args);
		}

		/// <summary>
		///   Updates the <paramref name="value" /> of the specified <paramref name="propertyName">property</paramref>
		/// </summary>
		/// <param name="value">the new value of the property</param>
		/// <param name="propertyName">the name of the property to set the new value</param>
		/// <param name="unifyStringValue">wether to remove extra whitespace and replace string.empty with null</param>
		/// <param name="raiseEvents">wether to raise the <see cref="PropertyChanged" /> event, if the property has changed</param>
		/// <param name="asyncEvents">
		///   wether <see cref="PropertyChanged" /> event is raised blocking in-thread,
		///   or non-blocking in a separate background thread
		/// </param>
		/// <returns>True, if the value has changed, else False</returns>
		protected virtual bool SetValue(object value, [CallerMemberName] string propertyName = null,
			bool unifyStringValue = true, bool raiseEvents = true, bool asyncEvents = false)
		{
			object curValue = null;
			lock (_propValues)
			{
				if (_propValues.ContainsKey(propertyName))
				{
					if (unifyStringValue)
					{
						var text = value as string;
						//empty string to null, remove surrounding whitespace
						if (text != null)
							value = string.Empty.Equals(text) ? null : text.Trim(' ', '\r', '\n', '\t');
					}
					curValue = _propValues[propertyName];
					if (Equals(curValue, value))
						return false;
					OnBeforePropertyChanging(propertyName, curValue, value);
					_propValues[propertyName] = value;
				}
				else
				{
					_propValues.Add(propertyName, value);
				}

				OnPropertyChanged(propertyName, curValue, value);

				if (raiseEvents)
				{
					// ReSharper disable ExplicitCallerInfoArgument
					if (asyncEvents)
						Task.Run(() => RaisePropertyChanged(propertyName));
					else
						RaisePropertyChanged(propertyName);
					// ReSharper restore ExplicitCallerInfoArgument
				}
				return true;
			}
		}

		/// <summary>
		///   Invoked, if a new value has been assigned to the specified <paramref name="propertyName">property</paramref>
		/// </summary>
		protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
		{
		}

		/// <summary>
		///   Invoked, before a new value has been assigned to the specified <paramref name="propertyName">property</paramref>
		/// </summary>
		protected virtual void OnBeforePropertyChanging(string propertyName, object oldValue, object newValue)
		{
		}

		protected T GetValue<T>(T defaultValue = default(T), [CallerMemberName] string propertyName = null)
		{
			lock (_propValues)
			{
				if (_propValues.ContainsKey(propertyName))
					return (T) _propValues[propertyName];
				return defaultValue;
			}
		}
	}
}