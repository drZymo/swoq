package main

import (
	"log"
	"os"
	"strconv"
)

func GetenvOptionalInt32(name string) *int32 {
	valueStr, exists := os.LookupEnv(name)
	if !exists {
		return nil
	}

	value, err := strconv.ParseInt(valueStr, 10, 32)
	if err != nil {
		log.Printf("Error parsing environment variable %s: %v", name, err)
		return nil
	}

	valueInt32 := int32(value)
	return &valueInt32
}
