   M S B A M L   `   `   `  ����G  CMeetNDiscuss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null+clr-namespace:MeetNDiscussMeetNDiscuss  5   	       MeetNDiscuss.MainWindow   5   	   W SPresentationCore, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=6.0.2.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=6.0.2.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   ,localclr-namespace:MeetNDiscuss     5   	   $	��600��5   	   $
��1000��6   $	��600��5	   	   $	��900��6   $��./Fonts/#nunito font)�5
   	     � 	Resources  5       �� MergedDictionaries �� Source�� 5   
    5      �� 5      $ /Styles/ColorPalette.xaml?�6%   �� 5      $ /Styles/Styles.xaml?�6%   %    5      .��5      � .+�5   
   � -   5         �Name$ HomeGrid��5      $y�Visible=�6!    � RowDefinitions 5      �� 5      $
��0.4* �6    �� 5      $��1* �6    .+�5      ��    MainScreenTextBlockStyle#���   6   $U��OMeet N' Discuss - лучший способ халявить на работе!��5      $��./Fonts/#oswald font)�5      $��31(�5        
BlackBrush#���  5      $��Center=�5      $��Bottom=�5       $��0 0 0 10q�5!      �� 5"       �� Orientation$ 
Horizontal=�6   $��Center=�5#      $	��Top=�5$      $��1��5%      .+�5&      �� -   5         ��Name$ StartDiscussionButton��5&        MainScreenButtonStyle#���  68   $��10q�6h   $��25(�6t     	GrayBrush#���  6�   $'��!Начать обсуждение��6�   6�   �� 5'      -   5        $ JoinDiscussionButton��5'      #���  67   $��10q�6g   $��25(�6s   #���  6�   $��Подключиться��6�   5(      5*      � 5,   
   -   5        $ RoomGrid��5,      $y�Visible=�6!   .+�5-      �� -   5         ��Name$ videoPlayer��5-      $��
10,0,10,70q�6)   �� 5.      -   5         ��Name$ comboBox��5.      $	��167��5/      $��21��50      $��	10,0,0,25q�51      $
��Left=�52      $��Bottom=�53      	 �� DisplayMemberPath$
	 Name��54      ��55      �� *VideoDevices+
 �� SelectedItem
 56      �� *CurrentDevice+�� 57      $	��167��6   $��24��58      $��	10,0,0,46q�59      $
��Left=�5:      $��Bottom=�5;      $��Select video source��5<      �� 5=      -   5        $��67��5=      $��21��5>      $��
213,0,0,25q�5?      $
��Left=�5@      $��Bottom=�5A      $��Start��5C      �� 5D      -   5        $��67��5D      $��21��5E      $��
296,0,0,25q�5F      $
��Left=�5G      $��Bottom=�5H      $
��Stop��5J      5K      5L      5M      