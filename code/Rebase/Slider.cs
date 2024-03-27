using Sandbox.UI.Construct;
using System;
using System.Linq;

namespace Sandbox.UI
{
	/// <summary>
	/// A horizontal slider. Can be float or whole number.
	/// </summary>
	public class Slider : Panel
	{
		public Panel Track { get; protected set; }
		public Panel TrackInner { get; protected set; }

		/// <summary>
		/// The right side of the slider
		/// </summary>
		public Vector2 ActualMouse { get; set; }=default;
		public float MaxValue { get; set; } = 100;

		/// <summary>
		/// The left side of the slider
		/// </summary>
		public float MinValue { get; set; } = 0;

		/// <summary>
		/// Default value for middle clicks. <see cref="float.NaN"/> means no default value.
		/// </summary>
		public float Default { get; set; } = float.NaN;

		/// <summary>
		/// If set to 1, value will be rounded to 1's
		/// If set to 10, value will be rounded to 10's
		/// If set to 0.1, value will be rounded to 0.1's
		/// </summary>
		public float Step { get; set; } = 1.0f;

		public Slider()
		{
			AddClass( "slider" );

			Track = Add.Panel( "track" );
			TrackInner = Track.Add.Panel( "inner" );
		}

		protected float _value = float.MaxValue;

		/// <summary>
		/// The actual value. Setting the value will snap and clamp it.
		/// </summary>
		public float Value
		{
			get => _value.Clamp( MinValue, MaxValue );
			set
			{
				var snapped = Step > 0 ?  value.SnapToGrid( Step ) : value;
				snapped = snapped.Clamp( MinValue, MaxValue );

				if ( _value == snapped ) return;

				_value = snapped;

				CreateEvent( "onchange" );
				CreateValueEvent( "value", _value );
				UpdateSliderPositions();
			}
		}

		public override void SetProperty( string name, string value )
		{
			if ( name == "min" && float.TryParse( value, out var floatValue ) )
			{
				MinValue = floatValue;
				UpdateSliderPositions();
				return;
			}

			if ( name == "step" && float.TryParse( value, out floatValue ) )
			{
				Step = floatValue;
				UpdateSliderPositions();
				return;
			}

			if ( name == "max" && float.TryParse( value, out floatValue ) )
			{
				MaxValue = floatValue;
				UpdateSliderPositions();
				return;
			}

			if ( name == "value" && float.TryParse( value, out floatValue ) )
			{
				Value = floatValue;
				return;
			}

			if ( name == "default" && float.TryParse( value, out floatValue ) )
			{
				Default = floatValue;
				return;
			}

			base.SetProperty( name, value );
		}

		/// <summary>
		/// Convert a screen position to a value. The value is clamped, but not snapped.
		/// </summary>
		public virtual float ScreenPosToValue( Vector2 pos )
		{
			var normalized = MathX.LerpInverse( pos.x, Track.Box.Left, Track.Box.Right, true );
			var scaled = MathX.LerpTo( MinValue, MaxValue, normalized, true );
			return Step > 0 ? scaled.SnapToGrid( Step ) : scaled;
		}
		/// <summary>
		/// If we move the mouse while we're being pressed then set the position,
		/// but skip transitions.
		/// </summary>
		protected override void OnMouseMove( MousePanelEvent e )
		{
			base.OnMouseMove( e );
			if ( !HasActive || e.MouseButton == MouseButtons.Middle ) return;
			
			Value = ScreenPosToValue( ActualMouse );
			UpdateSliderPositions();
			SkipTransitions();
			e.StopPropagation();
		}

		/// <summary>
		/// On mouse press jump to that position
		/// </summary>
		protected override void OnMouseDown( MousePanelEvent e )
		{
			base.OnMouseDown( e );
			Value = ScreenPosToValue(ActualMouse);
			//CreateEvent( "onmousedown", Value );
			UpdateSliderPositions();
			e.StopPropagation();
		}

		protected override void OnMouseUp( MousePanelEvent e )
		{
			base.OnMouseUp( e );
			Value = ScreenPosToValue( ActualMouse );
			CreateEvent( "onmousedown", Value );
			UpdateSliderPositions();
			e.StopPropagation();
		}

		protected override void OnMiddleClick( MousePanelEvent e )
		{
			base.OnMiddleClick( e );

			if ( !float.IsNaN( Default ) ) Value = Default;
			e.StopPropagation();
		}

		int positionHash;

		/// <summary>
		/// Updates the styles for TrackInner and Thumb to position us based on the current value.
		/// Note this purposely uses percentages instead of pixels when setting up, this way we don't
		/// have to worry about parent size, screen scale etc.
		/// </summary>
		void UpdateSliderPositions()
		{
			var hash = HashCode.Combine( Value, MinValue, MaxValue );
			if ( hash == positionHash ) return;

			positionHash = hash;

			var pos = MathX.LerpInverse( Value, MinValue, MaxValue, true );

			TrackInner.Style.Width = Length.Fraction( pos );

			TrackInner.Style.Dirty();
		}

	}

	namespace Construct
	{
		public static class SliderConstructor
		{
			public static Slider Slider( this PanelCreator self, float min, float max, float step )
			{
				var control = self.panel.AddChild<Slider>();
				control.MinValue = min;
				control.MaxValue = max;
				control.Step = step;

				return control;
			}
		}
	}
}
