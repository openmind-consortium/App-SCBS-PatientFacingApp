   M S B A M L   `   `   `  ���� @  <SCBS, Version=2.0.2.15, Culture=neutral, PublicKeyToken=null'clr-namespace:SCBS.Views
SCBS.Views  X TXceed.Wpf.Toolkit, Version=3.6.0.0, Culture=neutral, PublicKeyToken=3e4669d2f30244f4P:clr-namespace:Xceed.Wpf.Toolkit;assembly=Xceed.Wpf.ToolkitXceed.Wpf.Toolkit !    SCBS.Views.ReportWindowView   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     *localclr-namespace:SCBS.Views     NwpfTool:clr-namespace:Xceed.Wpf.Toolkit;assembly=Xceed.Wpf.Toolkit       � Title$*  $Report Medications and/or Conditions�� � WindowStyle$
 None=� � WindowState$ 	Maximized=� � WindowStartupLocation$ CenterScreen=�$	��700��$
��1200��$��24(�.���  � ColumnDefinitions �� $��* ��� $��* ��� $��* ��� $��* � � RowDefinitions �� $
��Auto ��� $��* ��� $
��Auto ��� $
��Auto ��� $
��Auto ��� $
��Auto ��� $��* ��� $	��100 �.+��� $��2��$��0��$��2��$6��0Choose one or more symptoms from the list below:��$��10 0 0 0q��� $��0��$��0��$��
Medication��$��10 0 0 0q��� -   $��listCondition��$��2��$��1��$��2��$��6��$��10q�$��Center=�$��Visible=����� *ConditionList+���� .R��� $��ChecksBoxZone��$��18(�$��Center=�$��Center=����� *	Condition+
 � Tag �� *Index+ d� 	IsChecked ��  �� Path$ 
IsSelected�	 �� Mode$	 TwoWay=�
 � LayoutTransform
 ��  �� ScaleX$ 2Q� �� ScaleY$ 2Q��� -   $��listMedication��$��0��$��1��$��2��$��1��$��10q�$��Center=�$��Visible=����� *MedicationList+���� .R��� $��CheckBoxZone��$��18(�$��Center=�$��Center=����� *
Medication+ �� *Index+ �� $ 
IsSelected�$	 TwoWay=�
 �� $ 2Q�$ 2Q��� $��0��$��2��$��2��$��Time last meds taken:��$��Center=�$
��Wrap=� �� TextAlignment$ Right=�$��0 0 10 0q�&   Xceed.Wpf.Toolkit.DateTimeUpDown  $��1��$��2��$��2��$	��150��$��0 0 20 0q�   Format$ 	ShortTime=��  �Xceed.Wpf.Toolkit.Primitives.UpDownBase`1[[System.Nullable`1[[System.DateTime, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]   	AllowSpin . ,  &Xceed.Wpf.Toolkit.Primitives.InputBase   TextAlignment$
 Left=�   ButtonSpinnerLocation$
 Left=�%   DisplayDefaultValueOnEmptyText .    MouseWheelActiveOnFocus .    ShowButtonSpinner . $��26(�   Value �� *MedicationTime+�� $��0��$��5��$��2��$(��"Add any additional comments below:��$��	10 10 0 0q��� -    ��Name$$ AdditionalCommentsForReportBox��$��0��$��6��$��2��$��1�� �� TextWrapping$
 Wrap=� �� AcceptsReturn . " �� VerticalScrollBarVisibility$
 Auto=�$��10q��� -    ��Name$ 	ExitClick��$��0��$��7��$��1��$��Exit Report��$��20 30 20 10q��� -   $ 
ResetClick��$��2��$��7��$��1��$��Reset��$��20 30 20 10q��� -   $ ReportClick��$��3��$��7��$��1��$��Submit��$��20 30 20 10q�