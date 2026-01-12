#include <xc.inc>


#define NUM_LEDS 27
#define LED_PIN 2			;**********

; WS2812B timings in ns:
; T0H 400
; T0L 850
; T1H 800
; T1L 450

; WS2812B timings in instruction cycles (48MHz clock):
#define T0H 5		; 4.8
#define T0L 10		; 10.2
#define T1H 10		; 9.6
#define T1L 5		; 5.4


GLOBAL _cnt, _red, _green, _blue, _Ticks

GLOBAL _reset_leds
SIGNAT _reset_leds,4217
GLOBAL _test_leds
SIGNAT _test_leds,4217
GLOBAL _set_led
SIGNAT _set_led,4217

PSECT code


_reset_leds:

		BANKSEL (TRISC)				; initialise LEDs
		bcf BANKMASK(TRISC),LED_PIN

		BANKSEL (LATC)					; reset LEDs
		bcf BANKMASK(LATC),LED_PIN

		BANKSEL (_cnt)
		clrf BANKMASK(_cnt)

res1:	clrwdt

		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop
		nop

		BANKSEL (_cnt)
		decfsz BANKMASK(_cnt)
		bra res1

		return


_test_leds:

		BANKSEL (_red)
		movlw 0x40
		movwf BANKMASK(_red)
		BANKSEL (_green)
		clrf BANKMASK(_green)
		BANKSEL (_blue)
		clrf BANKMASK(_blue)
		call test

		BANKSEL (_red)
		clrf BANKMASK(_red)
		BANKSEL (_green)
		movlw 0x40
		movwf BANKMASK(_green)
		BANKSEL (_blue)
		clrf BANKMASK(_blue)
		call test

		BANKSEL (_red)
		clrf BANKMASK(_red)
		BANKSEL (_green)
		clrf BANKMASK(_green)
		BANKSEL (_blue)
		movlw 0x40
		movwf BANKMASK(_blue)
		call test

		BANKSEL (_red)
		clrf BANKMASK(_red)
		BANKSEL (_green)
		clrf BANKMASK(_green)
		BANKSEL (_blue)
		clrf BANKMASK(_blue)
		call test

		return

test:
		call _reset_leds

		BANKSEL (_cnt)
		movlw NUM_LEDS
		movwf BANKMASK(_cnt)

test1:	call _set_led

		BANKSEL (_cnt)
		decfsz BANKMASK(_cnt)
		bra test1

		BANKSEL (_Ticks)
		clrf BANKMASK(_Ticks)

test2:	clrwdt

		BANKSEL (_Ticks)
		incf BANKMASK(_Ticks),w
		#define Z 2
		btfss STATUS,Z
		bra test2

		return


_set_led:

		BANKSEL (INTCON)				; disable interrupts
		bcf GIE

		clrw

rgb MACRO colour,bit

		BANKSEL (colour)
		btfsc BANKMASK(colour),bit
		movlw 1<<LED_PIN

		BANKSEL (LATC)
		bsf BANKMASK(LATC),LED_PIN

		nop							; T0H - 1
		nop
		nop
		nop

		movwf BANKMASK(LATC)

		clrw

		nop							; T1H - T0H - 2
		nop
		nop

		bcf BANKMASK(LATC),LED_PIN

									; T1L - 5

ENDM

		rgb _green,7
		rgb _green,6
		rgb _green,5
		rgb _green,4
		rgb _green,3
		rgb _green,2
		rgb _green,1
		rgb _green,0

		rgb _red,7
		rgb _red,6
		rgb _red,5
		rgb _red,4
		rgb _red,3
		rgb _red,2
		rgb _red,1
		rgb _red,0

		rgb _blue,7
		rgb _blue,6
		rgb _blue,5
		rgb _blue,4
		rgb _blue,3
		rgb _blue,2
		rgb _blue,1
		rgb _blue,0

		BANKSEL (INTCON)				; enable interrupts
		bsf GIE

		return
