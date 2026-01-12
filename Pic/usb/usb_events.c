#include "../system.h"

#include "usb.h"
#include "usb_device.h"
#include "usb_device_cdc.h"


void APP_LEDUpdateUSBStatus(void);
void APP_DeviceCDCEmulatorInitialize(void);
void LED_Flash(void);


bool USER_USB_CALLBACK_EVENT_HANDLER(USB_EVENT event, void *pdata, uint16_t size)
{
	switch ((int) event)
	{
	case EVENT_TRANSFER:
		break;

	case EVENT_SOF:
		// We are using the SOF as a timer to time the LED indicator.
//		APP_LEDUpdateUSBStatus();
		break;

	case EVENT_SUSPEND:
		// Update the LED status for the suspend event.
		APP_LEDUpdateUSBStatus();
		break;

	case EVENT_RESUME:
		// Update the LED status for the resume event.
		APP_LEDUpdateUSBStatus();
		break;

	case EVENT_CONFIGURED:
		// When the device is configured, we can (re)initialize the demo code.
		APP_DeviceCDCEmulatorInitialize();

		// double flash LED
		LED_Flash();
		LED_Flash();

		break;

	case EVENT_SET_DESCRIPTOR:
		break;

	case EVENT_EP0_REQUEST:
		// We have received a non-standard USB request. The HID driver
		// needs to check to see if the request was for it.
		USBCheckCDCRequest();
		break;

	case EVENT_BUS_ERROR:
		break;

	case EVENT_TRANSFER_TERMINATED:
		break;

	default:
		break;
	}

	return true;
}
